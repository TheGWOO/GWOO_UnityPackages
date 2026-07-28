using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWOO.Editor.ParticlePreview
{
	/// <summary>
	/// Owns all state + behavior for deterministic preview.
	/// Focus: deterministic reset/scrub + seed restore queue + save guard.
	/// </summary>
	internal sealed class ParticlePreviewSession
	{
		private const int MAX_RESTORE_ATTEMPTS = 10;
		private const int MAX_PENDING_RESET_ATTEMPTS = 10;

		private sealed class Entry
		{
			public ParticleSystem root;

			public float originAbsTime;
			public float lastLocalTime;

			public EditorParticleSystemDriver.Settings settings;
			public int contextKey;

			public ParticleSystem[] targets;

			public bool deterministicReady;

			public bool pendingReset;
			public float pendingLocalTime;
			public int pendingResetAttempts;

			public int RootId => root ? root.GetInstanceID() : 0;
		}

		private sealed class Baseline
		{
			public int id;
			public ParticleSystem ps;

			// captured once
			public bool useAuto;
			public uint seed;

			public bool wasDirty;
			public bool wasPrefabInstance;

			public PropertyModification[] seedPrefabModsBefore;

			// touched by driver
			public bool touchedSeed;
			public bool touchedAuto;

			public uint appliedSeed;
			public bool appliedUseAuto;

			public bool TouchedAny => touchedSeed || touchedAuto;
		}

		private bool _sessionActive;
		private int _sessionKey;
		private int _activeContextKey;

		private readonly Dictionary<int, Entry> _entriesByRootId = new(64);
		private readonly Dictionary<int, Baseline> _baselineByTargetId = new(128);

		private readonly List<int> _restoreQueue = new(128);
		private readonly HashSet<int> _restoreQueueSet = new();
		private readonly Dictionary<int, int> _restoreAttemptsById = new(128);
		private bool _restoreQueued;

		private readonly List<int> _pendingResetQueue = new(64);
		private readonly HashSet<int> _pendingResetSet = new();
		private bool _pendingResetQueued;

		// Save/apply guard
		private bool _saveGuardActive;
		private bool _reapplyAfterSaveQueued;

		// --------------------
		// Session API
		// --------------------

		public void BeginSession(int sessionKey)
		{
			if (_sessionActive && _sessionKey == sessionKey)
				return;

			if (_sessionActive && _sessionKey != sessionKey)
				EndSession(clearParticles: true);

			_sessionActive = true;
			_sessionKey = sessionKey;
			_activeContextKey = 0;

			_entriesByRootId.Clear();
			_baselineByTargetId.Clear();

			_restoreQueue.Clear();
			_restoreQueueSet.Clear();
			_restoreAttemptsById.Clear();
			_restoreQueued = false;

			_pendingResetQueue.Clear();
			_pendingResetSet.Clear();
			_pendingResetQueued = false;

			_saveGuardActive = false;
			_reapplyAfterSaveQueued = false;
		}

		public void EndSession(bool clearParticles = true)
		{
			// IMPORTANT:
			// Even if _sessionActive is false (because caller just unbound),
			// we may still have touched baselines that must be restored before Save / Reload.
			if (!_sessionActive && !HasTouchedBaselines())
				return;

			CancelDelayedWork();

			if (clearParticles)
			{
				foreach (Entry e in _entriesByRootId.Values)
				{
					if (e?.root)
						ParticleSystemUtils.StopAndClear(e.root, includeChildren: true);
				}
			}

			// Hard restore: synchronous, no delayCall.
			RestoreAllTouchedBaselinesNow(clearParticlesFirst: false);

			_entriesByRootId.Clear();

			_activeContextKey = 0;
			_sessionKey = 0;
			_sessionActive = false;

			_pendingResetQueue.Clear();
			_pendingResetSet.Clear();
			_pendingResetQueued = false;

			_saveGuardActive = false;
			_reapplyAfterSaveQueued = false;
		}

		// --------------------
		// Context API
		// --------------------

		public void SetContextKey(int contextKey, bool clearParticlesOnExit = true)
		{
			if (!_sessionActive)
				return;

			if (contextKey == _activeContextKey)
				return;

			if (_activeContextKey != 0)
				FlushContext(_activeContextKey, clearParticlesOnExit);

			_activeContextKey = contextKey;
		}

		// --------------------
		// Runtime API
		// --------------------

		public void RegisterOrUpdate(
			ParticleSystem ps,
			float originAbsTime,
			bool restartNow,
			in EditorParticleSystemDriver.Settings settings,
			int contextKey)
		{
			if (!ps) return;
			if (Application.isPlaying) return;

			if (!_sessionActive)
				BeginSession(0xC0FFEE);

			SetContextKey(contextKey, clearParticlesOnExit: true);

			int rootId = ps.GetInstanceID();

			bool isNew = !_entriesByRootId.TryGetValue(rootId, out Entry e);
			if (isNew)
			{
				e = new Entry();
				_entriesByRootId.Add(rootId, e);
			}

			originAbsTime = Mathf.Max(0f, originAbsTime);

			bool rootChanged = e.root != ps;
			bool settingsChanged = !e.settings.Equals(settings);
			bool originChanged = !Mathf.Approximately(e.originAbsTime, originAbsTime);
			
			e.root = ps;
			e.originAbsTime = originAbsTime;
			e.settings = settings;
			e.contextKey = contextKey;

			if (isNew || rootChanged || settingsChanged)
			{
				RebuildTargets(e);
				e.deterministicReady = false;
			}

			if (isNew || restartNow || rootChanged || settingsChanged || originChanged)
			{
				e.lastLocalTime = 0f;
				RequestResetAndSimTo(e, targetLocalTime: 0f);
			}
			else
			{
				e.lastLocalTime = Mathf.Max(0f, e.lastLocalTime);
			}
		}

		public void Unregister(ParticleSystem ps, bool clearNow)
		{
			if (!ps) return;
			if (Application.isPlaying) return;

			int id = ps.GetInstanceID();

			if (_entriesByRootId.TryGetValue(id, out Entry e))
			{
				if (clearNow && e?.root)
					ParticleSystemUtils.StopAndClear(e.root, includeChildren: true);

				RestoreEntryTargets(e);
				_entriesByRootId.Remove(id);

				ProcessRestoreQueueNowOrLater();
				return;
			}

			if (clearNow)
				ParticleSystemUtils.StopAndClear(ps, includeChildren: true);
		}

		public void Advance(float dtAbs)
		{
			if (Application.isPlaying) return;
			if (!_sessionActive) return;
			if (dtAbs <= 0f) return;

			CleanupDeadEntries();

			foreach (Entry e in _entriesByRootId.Values)
			{
				if (e?.root == null) continue;
				if (e.contextKey != _activeContextKey) continue;

				if (e.pendingReset)
				{
					e.pendingLocalTime = Mathf.Max(0f, e.pendingLocalTime + dtAbs);
					TryCompletePendingReset(e);
					continue;
				}

				e.root.Simulate(dtAbs, e.settings.includeChildren, restart: false, fixedTimeStep: e.settings.fixedTimeStep);
				e.lastLocalTime += dtAbs;
			}
		}

		public void Seek(float absoluteTime)
		{
			if (Application.isPlaying) return;
			if (!_sessionActive) return;

			absoluteTime = Mathf.Max(0f, absoluteTime);

			CleanupDeadEntries();

			foreach (Entry e in _entriesByRootId.Values)
			{
				if (e?.root == null) continue;
				if (e.contextKey != _activeContextKey) continue;

				float local = Mathf.Max(0f, absoluteTime - e.originAbsTime);
				SampleLocal(e, local);
			}
		}

		// --------------------
		// Save hooks API
		// --------------------

		public void NotifyWillSaveAssets(string[] paths) => BeginSaveGuard();

		public void OnSceneSaving(Scene scene, string path) => BeginSaveGuard();

		public void OnSceneSaved(Scene scene) => QueueReapplyAfterSave();

		public void OnPrefabStageSaving(GameObject prefabRoot) => BeginSaveGuard();

		public void OnPrefabStageSaved(GameObject prefabRoot) => QueueReapplyAfterSave();

		// --------------------
		// Editor lifecycle
		// --------------------

		public void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode ||
			    state == PlayModeStateChange.EnteredPlayMode)
			{
				EndSession(clearParticles: true);
			}
		}

		public void OnEditorQuitting()
		{
			EndSession(clearParticles: true);
		}

		public void OnBeforeAssemblyReload()
		{
			EndSession(clearParticles: true);
		}

		// --------------------
		// Internals
		// --------------------

		private void RebuildTargets(Entry e)
		{
			if (e == null || !e.root)
			{
				if (e != null) e.targets = null;
				return;
			}

			e.targets = e.settings.includeChildren
				? e.root.GetComponentsInChildren<ParticleSystem>(true)
				: new[] { e.root };
		}

		private void FlushContext(int contextKey, bool clearParticles)
		{
			List<int> toRemove = null;

			foreach (KeyValuePair<int, Entry> kvp in _entriesByRootId)
			{
				Entry e = kvp.Value;
				if (e == null) continue;
				if (e.contextKey != contextKey) continue;

				if (clearParticles && e.root)
					ParticleSystemUtils.StopAndClear(e.root, includeChildren: true);

				RestoreEntryTargets(e);

				toRemove ??= new List<int>(16);
				toRemove.Add(kvp.Key);
			}

			if (toRemove != null)
			{
				for (int i = 0; i < toRemove.Count; i++)
					_entriesByRootId.Remove(toRemove[i]);

				ProcessRestoreQueueNowOrLater();
			}
		}

		private void CleanupDeadEntries()
		{
			List<int> dead = null;

			foreach (KeyValuePair<int, Entry> kvp in _entriesByRootId)
			{
				Entry e = kvp.Value;
				if (e == null || !e.root)
				{
					dead ??= new List<int>(16);
					dead.Add(kvp.Key);
				}
			}

			if (dead != null)
			{
				for (int i = 0; i < dead.Count; i++)
					_entriesByRootId.Remove(dead[i]);
			}
		}

		private void SampleLocal(Entry e, float localTime)
		{
			if (e.pendingReset)
			{
				e.pendingLocalTime = Mathf.Max(0f, localTime);
				TryCompletePendingReset(e);
				return;
			}

			if (localTime >= e.lastLocalTime)
			{
				float dt = localTime - e.lastLocalTime;
				if (dt > 0f)
					e.root.Simulate(dt, e.settings.includeChildren, restart: false, fixedTimeStep: e.settings.fixedTimeStep);

				e.lastLocalTime = localTime;
				return;
			}

			if (!e.settings.deterministicSeed || e.deterministicReady)
			{
				RestartAndSimulateTo(e, localTime);
				return;
			}

			RequestResetAndSimTo(e, localTime);
		}

		private void RequestResetAndSimTo(Entry e, float targetLocalTime)
		{
			if (e == null || !e.root)
				return;

			targetLocalTime = Mathf.Max(0f, targetLocalTime);

			if (e.settings.deterministicSeed && e.deterministicReady)
			{
				RestartAndSimulateTo(e, targetLocalTime);
				return;
			}

			ParticleSystemUtils.StopAndClear(e.root, includeChildren: e.settings.includeChildren);
			e.lastLocalTime = 0f;

			if (!e.settings.deterministicSeed)
			{
				RestartAndSimulateTo(e, targetLocalTime);
				return;
			}

			e.pendingReset = true;
			e.pendingLocalTime = targetLocalTime;
			e.pendingResetAttempts = 0;

			EnqueuePendingReset(e.RootId);

			TryCompletePendingReset(e);
			ProcessPendingResetQueueNowOrLater();
		}

		private void RestartAndSimulateTo(Entry e, float targetLocalTime)
		{
			if (e == null || !e.root)
				return;

			targetLocalTime = Mathf.Max(0f, targetLocalTime);

			try
			{
				e.root.Simulate(0f, e.settings.includeChildren, restart: true, fixedTimeStep: true);
				e.root.time = 0f;
			}
			catch { }

			if (e.settings.manualBurstOnReset)
			{
				try { e.root.Emit(Mathf.Max(1, e.settings.burstCount)); }
				catch { }
			}

			if (targetLocalTime > 0f)
			{
				try
				{
					e.root.Simulate(targetLocalTime, e.settings.includeChildren, restart: false,
						fixedTimeStep: e.settings.fixedTimeStep);
				}
				catch { }
			}

			e.lastLocalTime = targetLocalTime;
		}

		// --------------------
		// Pending reset queue
		// --------------------

		private void EnqueuePendingReset(int rootId)
		{
			if (rootId == 0) return;

			if (_pendingResetSet.Add(rootId))
				_pendingResetQueue.Add(rootId);
		}

		private void ProcessPendingResetQueueNowOrLater()
		{
			if (_pendingResetQueue.Count == 0)
				return;

			if (_pendingResetQueued)
				return;

			_pendingResetQueued = true;
			EditorApplication.delayCall += ProcessPendingResetQueue;
		}

		private void ProcessPendingResetQueue()
		{
			_pendingResetQueued = false;

			if (_pendingResetQueue.Count == 0)
			{
				_pendingResetSet.Clear();
				return;
			}

			int count = _pendingResetQueue.Count;
			int[] ids = new int[count];
			for (int i = 0; i < count; i++)
				ids[i] = _pendingResetQueue[i];

			_pendingResetQueue.Clear();
			_pendingResetSet.Clear();

			for (int i = 0; i < ids.Length; i++)
			{
				if (!_entriesByRootId.TryGetValue(ids[i], out Entry e) || e == null || !e.root)
					continue;

				if (!e.pendingReset)
					continue;

				TryCompletePendingReset(e);
			}

			if (_pendingResetQueue.Count > 0)
				ProcessPendingResetQueueNowOrLater();
		}

		private void TryCompletePendingReset(Entry e)
		{
			if (e == null || !e.root || !e.pendingReset)
				return;

			ParticleSystemUtils.StopAndClear(e.root, includeChildren: e.settings.includeChildren);

			if (e.targets == null || e.targets.Length == 0)
				RebuildTargets(e);

			ParticleSystem[] systems = e.targets;
			if (systems != null)
			{
				for (int i = 0; i < systems.Length; i++)
				{
					ParticleSystem ps = systems[i];
					if (!ps) continue;

					if (!ParticleSystemUtils.IsDefinitelyStopped(ps))
					{
						e.pendingResetAttempts++;

						if (e.pendingResetAttempts >= MAX_PENDING_RESET_ATTEMPTS)
						{
							e.pendingReset = false;
							e.pendingResetAttempts = 0;
							e.deterministicReady = false;
							RestartAndSimulateTo(e, e.pendingLocalTime);
						}
						else
						{
							EnqueuePendingReset(e.RootId);
							ProcessPendingResetQueueNowOrLater();
						}

						return;
					}
				}
			}

			ApplyDeterministicSeedNow(e);

			e.pendingReset = false;
			e.pendingResetAttempts = 0;

			RestartAndSimulateTo(e, e.pendingLocalTime);
		}

		private void ApplyDeterministicSeedNow(Entry e)
		{
			ParticleSystem[] systems = e.targets;
			if (systems == null || systems.Length == 0)
			{
				e.deterministicReady = false;
				return;
			}

			uint baseSeed = e.settings.seed;

			for (int i = 0; i < systems.Length; i++)
			{
				ParticleSystem ps = systems[i];
				if (!ps) continue;

				if (!ParticleSystemUtils.IsDefinitelyStopped(ps))
					continue;

				uint desiredSeed = e.settings.includeChildren
					? ParticleSystemUtils.MixSeed(baseSeed, ps.GetInstanceID())
					: baseSeed;

				bool gotAuto = ParticleSystemUtils.TryGetUseAuto(ps, out bool curAuto);
				bool gotSeed = ParticleSystemUtils.TryGetSeed(ps, out uint curSeed);

				bool needsChange = !gotAuto || curAuto || !gotSeed || curSeed != desiredSeed;
				if (!needsChange)
					continue;

				CaptureBaselineIfNeeded(ps);

				try
				{
					ps.randomSeed = desiredSeed;
					ps.useAutoRandomSeed = false;

					MarkTouched(ps, appliedSeed: desiredSeed, appliedUseAuto: false);
				}
				catch { }
			}

			e.deterministicReady = AreDeterministicSeedsApplied(e);
		}

		private bool AreDeterministicSeedsApplied(Entry e)
		{
			if (e == null || e.targets == null || e.targets.Length == 0)
				return false;

			uint baseSeed = e.settings.seed;

			for (int i = 0; i < e.targets.Length; i++)
			{
				ParticleSystem ps = e.targets[i];
				if (!ps) continue;

				uint desiredSeed = e.settings.includeChildren
					? ParticleSystemUtils.MixSeed(baseSeed, ps.GetInstanceID())
					: baseSeed;

				if (!ParticleSystemUtils.TryGetUseAuto(ps, out bool curAuto)) return false;
				if (curAuto) return false;

				if (!ParticleSystemUtils.TryGetSeed(ps, out uint curSeed)) return false;
				if (curSeed != desiredSeed) return false;
			}

			return true;
		}

		private void CaptureBaselineIfNeeded(ParticleSystem ps)
		{
			if (!ps) return;

			int id = ps.GetInstanceID();
			if (_baselineByTargetId.ContainsKey(id))
				return;

			bool wasPrefabInstance = PrefabOverridesUtils.IsScenePrefabInstance(ps);

			var b = new Baseline
			{
				id = id,
				ps = ps,
				useAuto = ParticleSystemUtils.SafeGetUseAuto(ps),
				seed = ParticleSystemUtils.SafeGetSeed(ps),
				wasDirty = EditorUtility.IsDirty(ps),
				wasPrefabInstance = wasPrefabInstance,
				seedPrefabModsBefore = null,
				touchedSeed = false,
				touchedAuto = false,
				appliedSeed = 0,
				appliedUseAuto = false
			};

			if (wasPrefabInstance)
			{
				try
				{
					PropertyModification[] mods = PrefabUtility.GetPropertyModifications(ps);
					b.seedPrefabModsBefore = PrefabOverridesUtils.ExtractSeedPrefabMods(mods);
				}
				catch
				{
					b.seedPrefabModsBefore = null;
				}
			}

			_baselineByTargetId.Add(id, b);
		}

		private void MarkTouched(ParticleSystem ps, uint appliedSeed, bool appliedUseAuto)
		{
			if (!ps) return;

			int id = ps.GetInstanceID();
			if (!_baselineByTargetId.TryGetValue(id, out Baseline b))
				return;

			b.touchedSeed = true;
			b.touchedAuto = true;
			b.appliedSeed = appliedSeed;
			b.appliedUseAuto = appliedUseAuto;
		}

		// --------------------
		// Restore queue
		// --------------------

		private void RestoreEntryTargets(Entry e)
		{
			if (e?.targets == null)
				return;

			for (int i = 0; i < e.targets.Length; i++)
			{
				ParticleSystem ps = e.targets[i];
				if (!ps) continue;

				int id = ps.GetInstanceID();
				if (_baselineByTargetId.TryGetValue(id, out Baseline b) && b.TouchedAny)
					EnqueueRestore(id);
			}
		}

		private void EnqueueRestore(int id)
		{
			if (_restoreQueueSet.Add(id))
				_restoreQueue.Add(id);
		}

		private void ProcessRestoreQueueNowOrLater()
		{
			if (_restoreQueue.Count == 0)
				return;

			if (_restoreQueued)
				return;

			_restoreQueued = true;
			EditorApplication.delayCall += ProcessRestoreQueue;
		}

		private void ProcessRestoreQueue()
		{
			_restoreQueued = false;

			if (_restoreQueue.Count == 0)
			{
				_restoreQueueSet.Clear();
				return;
			}

			int count = _restoreQueue.Count;
			int[] ids = new int[count];
			for (int i = 0; i < count; i++)
				ids[i] = _restoreQueue[i];

			_restoreQueue.Clear();
			_restoreQueueSet.Clear();

			for (int i = 0; i < ids.Length; i++)
				TryRestoreBaseline(ids[i]);

			if (_restoreQueue.Count > 0)
				ProcessRestoreQueueNowOrLater();
		}

		private void TryRestoreBaseline(int id)
		{
			if (!_baselineByTargetId.TryGetValue(id, out Baseline b))
				return;

			if (!b.TouchedAny)
			{
				_baselineByTargetId.Remove(id);
				_restoreAttemptsById.Remove(id);
				return;
			}

			ParticleSystem ps = b.ps;
			if (!ps)
			{
				ps = EditorUtility.EntityIdToObject(id) as ParticleSystem;
				if (!ps)
				{
					_baselineByTargetId.Remove(id);
					_restoreAttemptsById.Remove(id);
					return;
				}

				b.ps = ps;
			}

			_restoreAttemptsById.TryGetValue(id, out int attempts);

			if (attempts >= MAX_RESTORE_ATTEMPTS)
			{
				_baselineByTargetId.Remove(id);
				_restoreAttemptsById.Remove(id);
				return;
			}

			ParticleSystemUtils.StopAndClear(ps, includeChildren: true);

			// First pass yields a frame for Unity to settle stopped state.
			if (attempts == 0)
			{
				_restoreAttemptsById[id] = attempts + 1;
				EnqueueRestore(id);
				return;
			}

			if (!ParticleSystemUtils.IsDefinitelyStopped(ps))
			{
				_restoreAttemptsById[id] = attempts + 1;
				EnqueueRestore(id);
				return;
			}

			if (!ShouldRestoreTouchedProperties(ps, b))
			{
				_baselineByTargetId.Remove(id);
				_restoreAttemptsById.Remove(id);
				return;
			}

			try
			{
				ps.randomSeed = b.seed;
				ps.useAutoRandomSeed = b.useAuto;

				if (b.wasPrefabInstance)
					PrefabOverridesUtils.RestoreSeedPrefabModsToBaseline(ps, b.seedPrefabModsBefore);

				if (!b.wasDirty)
					EditorUtility.ClearDirty(ps);

				_baselineByTargetId.Remove(id);
				_restoreAttemptsById.Remove(id);
			}
			catch
			{
				_restoreAttemptsById[id] = attempts + 1;
				EnqueueRestore(id);
			}
		}

		private static bool ShouldRestoreTouchedProperties(ParticleSystem ps, Baseline b)
		{
			if (!ps) return false;

			if (!ParticleSystemUtils.TryGetUseAuto(ps, out bool curAuto)) return false;
			if (!ParticleSystemUtils.TryGetSeed(ps, out uint curSeed)) return false;

			if (b.touchedAuto && curAuto != b.appliedUseAuto) return false;
			if (b.touchedSeed && curSeed != b.appliedSeed) return false;

			return true;
		}

		// --------------------
		// Save/apply safeguards
		// --------------------

		private void BeginSaveGuard()
		{
			if (Application.isPlaying)
				return;

			// If the session is inactive but baselines exist, we *still* need to clean up before saving.
			if (!_sessionActive && !HasTouchedBaselines())
				return;

			CancelDelayedWork();

			if (!HasTouchedBaselines())
				return;

			_saveGuardActive = true;

			// Hard restore NOW, then optionally reapply if we're still active.
			RestoreAllTouchedBaselinesNow(clearParticlesFirst: true);

			if (_sessionActive)
				QueueReapplyAfterSave();
			else
			{
				// Session is ended, so we don't reapply anything.
				_saveGuardActive = false;
				_reapplyAfterSaveQueued = false;
			}
		}

		private void QueueReapplyAfterSave()
		{
			if (!_sessionActive) return;
			if (!_saveGuardActive) return;

			if (_reapplyAfterSaveQueued)
				return;

			_reapplyAfterSaveQueued = true;
			EditorApplication.delayCall += ReapplyAfterSaveDelayCall;
		}

		private void ReapplyAfterSaveDelayCall()
		{
			EditorApplication.delayCall -= ReapplyAfterSaveDelayCall;

			_reapplyAfterSaveQueued = false;
			_saveGuardActive = false;

			if (!_sessionActive)
				return;

			foreach (Entry e in _entriesByRootId.Values)
			{
				if (e == null || !e.root)
					continue;

				e.deterministicReady = false;
				e.pendingReset = false;
				e.pendingResetAttempts = 0;

				RequestResetAndSimTo(e, e.lastLocalTime);
			}
		}
		
		private void RestoreAllTouchedBaselinesNow(bool clearParticlesFirst)
		{
			// We only restore what we touched, and only if the user didn't change it after our apply.
			// This is the key to "don't clobber user intent".

			foreach (Baseline b in _baselineByTargetId.Values)
			{
				if (!b.TouchedAny)
					continue;

				ParticleSystem ps = b.ps ? b.ps : (EditorUtility.EntityIdToObject(b.id) as ParticleSystem);
				if (!ps)
					continue;

				if (clearParticlesFirst)
					ParticleSystemUtils.StopAndClear(ps, includeChildren: true);

				if (!ShouldRestoreTouchedProperties(ps, b))
					continue;

				try
				{
					ps.randomSeed = b.seed;
					ps.useAutoRandomSeed = b.useAuto;

					if (b.wasPrefabInstance)
						PrefabOverridesUtils.RestoreSeedPrefabModsToBaseline(ps, b.seedPrefabModsBefore);

					if (!b.wasDirty)
						EditorUtility.ClearDirty(ps);
				}
				catch
				{
					// Best effort. For hard-exit/save we prefer to try once and move on.
				}
			}

			// After a "hard" restore, drop baselines (we're done / shutting down).
			// (Untouched baselines can be dropped too.)
			_baselineByTargetId.Clear();
			_restoreAttemptsById.Clear();

			_restoreQueue.Clear();
			_restoreQueueSet.Clear();
		}
		
		private bool HasTouchedBaselines()
		{
			foreach (Baseline b in _baselineByTargetId.Values)
			{
				if (b.TouchedAny)
					return true;
			}

			return false;
		}

		private void CancelDelayedWork()
		{
			if (_restoreQueued)
				EditorApplication.delayCall -= ProcessRestoreQueue;

			if (_pendingResetQueued)
				EditorApplication.delayCall -= ProcessPendingResetQueue;

			if (_reapplyAfterSaveQueued)
				EditorApplication.delayCall -= ReapplyAfterSaveDelayCall;

			_restoreQueued = false;
			_pendingResetQueued = false;
			_reapplyAfterSaveQueued = false;
		}
	}
}
