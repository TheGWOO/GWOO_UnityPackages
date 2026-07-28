using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.Animations;
using UnityEngine;

namespace GWOO.Editor.Tools
{
	internal sealed class PreviewAnimationStates
	{
		#region Fields

		private readonly AnimatorPreviewerState _previewerState;

		private readonly Dictionary<StateKey, string> _stateNameByKey = new();
		private readonly List<AnimatorPreviewerStateEntry> _states = new();
		private readonly List<AnimatorPreviewerStateEntry> _statesVisible = new();

		// Instance map (safer than static).
		private readonly Dictionary<AnimationClip, AnimationClip> _clipOverrideMap = new();

		private static readonly List<KeyValuePair<AnimationClip, AnimationClip>> SCRATCH_OVERRIDES = new(256);
		private static readonly List<string> SCRATCH_TOKENS_REQUIRED = new(16);
		private static readonly List<string> SCRATCH_TOKENS_EXCLUDED = new(16);

		#endregion Fields

		#region Properties

		internal int TotalCount => _states.Count;
		internal int VisibleCount => _statesVisible.Count;

		#endregion Properties

		#region Constructors

		internal PreviewAnimationStates(PreviewContext ctx)
		{
			_previewerState = ctx.State;
		}

		#endregion Constructors

		#region Methods

		internal IReadOnlyList<AnimatorPreviewerStateEntry> GetVisibleStates() => _statesVisible;

		internal string ResolveStateName(int layer, int fullPathHash)
		{
			if (fullPathHash == 0)
				return "(Unknown)";

			return _stateNameByKey.TryGetValue(new StateKey { layer = layer, stateHash = fullPathHash }, out string stateName)
				? stateName
				: $"(hash {fullPathHash})";
		}

		internal void RefreshStates()
		{
			RebuildClipOverrideMap();
			CacheStates();
			ApplyStateFilter();
		}

		internal void ApplyStateFilter()
		{
			_statesVisible.Clear();

			string query = _previewerState.stateSearch ?? string.Empty;

			List<string> required = SCRATCH_TOKENS_REQUIRED;
			List<string> excluded = SCRATCH_TOKENS_EXCLUDED;

			required.Clear();
			excluded.Clear();

			ParseSearchTokens(query, required, excluded);

			if (required.Count == 0 && excluded.Count == 0)
			{
				_statesVisible.AddRange(_states);
				return;
			}

			for (int i = 0; i < _states.Count; i++)
			{
				AnimatorPreviewerStateEntry entry = _states[i];
				if (StateMatchesQuery(entry.normalizedSearchKey, required, excluded))
					_statesVisible.Add(entry);
			}
		}

		internal void RefreshOverrideMapOnly()
		{
			RebuildClipOverrideMap();
		}

		internal AnimationClip ResolveOverriddenClip(AnimationClip clip)
		{
			if (clip == null)
				return null;

			// Multi-hop resolution (A->B->C), guarded against cycles.
			for (int guard = 0; guard < 16; guard++)
			{
				if (!_clipOverrideMap.TryGetValue(clip, out AnimationClip next) || next == null || next == clip)
					return clip;

				clip = next;
			}

			return clip;
		}

		private void RebuildClipOverrideMap()
		{
			_clipOverrideMap.Clear();

			if (!_previewerState.TryGetResolvedOverrideController(out AnimatorOverrideController controller) || controller == null)
				return;

			// Walk override chain if nested (AOC -> AOC -> AnimatorController).
			for (AnimatorOverrideController current = controller; current != null; current = current.runtimeAnimatorController as AnimatorOverrideController)
			{
				SCRATCH_OVERRIDES.Clear();
				current.GetOverrides(SCRATCH_OVERRIDES);

				for (int i = 0; i < SCRATCH_OVERRIDES.Count; i++)
				{
					AnimationClip original = SCRATCH_OVERRIDES[i].Key;
					AnimationClip replacement = SCRATCH_OVERRIDES[i].Value;

					if (original == null || replacement == null)
						continue;

					_clipOverrideMap[original] = replacement;
				}
			}
		}

		private Motion ResolveMotionOverrides(Motion motion)
		{
			if (motion is AnimationClip clip)
				return ResolveOverriddenClip(clip);

			return motion;
		}

		private void CacheStates()
		{
			_states.Clear();
			_stateNameByKey.Clear();

			AnimatorController controller = _previewerState.ResolvedTargetController;
			if (controller == null)
				return;

			for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
			{
				AnimatorControllerLayer layer = controller.layers[layerIndex];
				CollectStatesRecursive(layer.stateMachine, layerIndex, layer.name, layer.name, _states);
			}

			for (int i = 0; i < _states.Count; i++)
			{
				AnimatorPreviewerStateEntry entry = _states[i];
				_stateNameByKey[new StateKey { layer = entry.layerIndex, stateHash = entry.stateHash }] = entry.fullPath;
			}

			_states.Sort((a, b) => string.Compare(a.fullPath, b.fullPath, StringComparison.OrdinalIgnoreCase));
		}

		private void CollectStatesRecursive(
			AnimatorStateMachine stateMachine,
			int layerIndex,
			string layerName,
			string prefix,
			List<AnimatorPreviewerStateEntry> list)
		{
			foreach (ChildAnimatorState childState in stateMachine.states)
			{
				AnimatorState state = childState.state;

				string relativePath = string.IsNullOrEmpty(prefix) ? state.name : $"{prefix}/{state.name}";
				string fullPathForHash = relativePath.Replace('/', '.');

				Motion effectiveMotion = ResolveMotionOverrides(state.motion);

				list.Add(new AnimatorPreviewerStateEntry
				{
					fullPath = relativePath,
					layerName = layerName,
					leafName = state.name,
					normalizedSearchKey = BuildNormalizedSearchKey(relativePath, layerName, effectiveMotion),
					layerIndex = layerIndex,
					stateHash = Animator.StringToHash(fullPathForHash),
					motion = effectiveMotion
				});
			}

			foreach (ChildAnimatorStateMachine subMachine in stateMachine.stateMachines)
			{
				string nextPrefix = string.IsNullOrEmpty(prefix)
					? subMachine.stateMachine.name
					: $"{prefix}/{subMachine.stateMachine.name}";

				CollectStatesRecursive(subMachine.stateMachine, layerIndex, layerName, nextPrefix, list);
			}
		}

		private string BuildNormalizedSearchKey(string relativePath, string layerName, Motion motion)
		{
			StringBuilder sb = new(128);

			if (!string.IsNullOrEmpty(relativePath))
				sb.Append(relativePath);

			if (!string.IsNullOrEmpty(layerName))
			{
				sb.Append(' ');
				sb.Append(layerName);
			}

			AppendMotionSearchTerms(sb, motion, 0);

			return NormalizeForSearch(sb.ToString());
		}

		private void AppendMotionSearchTerms(StringBuilder sb, Motion motion, int depth)
		{
			if (motion == null || depth > 8)
				return;

			if (!string.IsNullOrEmpty(motion.name))
			{
				sb.Append(' ');
				sb.Append(motion.name);
			}

			if (motion is AnimationClip clip)
			{
				AnimationClip resolved = ResolveOverriddenClip(clip);
				if (resolved != null && resolved != clip && !string.IsNullOrEmpty(resolved.name))
				{
					sb.Append(' ');
					sb.Append(resolved.name);
				}

				return;
			}

			if (motion is not BlendTree blendTree)
				return;

			ChildMotion[] children = blendTree.children;
			if (children == null || children.Length == 0)
				return;

			for (int i = 0; i < children.Length; i++)
				AppendMotionSearchTerms(sb, children[i].motion, depth + 1);
		}

		private static void ParseSearchTokens(string query, List<string> required, List<string> excluded)
		{
			if (string.IsNullOrWhiteSpace(query))
				return;

			string[] split = query.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < split.Length; i++)
			{
				string term = split[i].Trim();
				if (term.Length == 0)
					continue;

				if (term.StartsWith("-", StringComparison.Ordinal) && term.Length > 1)
					excluded.Add(term[1..]);
				else
					required.Add(term);
			}
		}

		private static bool StateMatchesQuery(string normalizedHaystack, List<string> required, List<string> excluded)
		{
			if (string.IsNullOrEmpty(normalizedHaystack))
				return false;

			for (int i = 0; i < excluded.Count; i++)
			{
				if (normalizedHaystack.Contains(excluded[i]))
					return false;
			}

			for (int i = 0; i < required.Count; i++)
			{
				if (!normalizedHaystack.Contains(required[i]))
					return false;
			}

			return true;
		}

		private static string NormalizeForSearch(string s)
		{
			if (string.IsNullOrEmpty(s))
				return string.Empty;

			StringBuilder sb = new(s.Length * 2);
			char prev = '\0';

			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];

				if (c == '_' || c == '-' || c == '/' || c == '.' || c == '\\')
				{
					sb.Append(' ');
					prev = c;
					continue;
				}

				if (char.IsUpper(c) && prev != '\0' && char.IsLetter(prev) && char.IsLower(prev))
					sb.Append(' ');

				sb.Append(char.ToLowerInvariant(c));
				prev = c;
			}

			return sb.ToString();
		}

		#endregion Methods
	}
}

