using UnityEngine;

namespace GWOO.Editor.Tools
{
	public readonly struct EventSignature
	{
		public readonly float time;
		public readonly string functionName;
		public readonly float floatParameter;
		public readonly int intParameter;
		public readonly string stringParameter;
		public readonly int objectId;

		public EventSignature(AnimationEvent animEvent)
		{
			time = animEvent != null ? animEvent.time : 0f;
			functionName = animEvent != null ? (animEvent.functionName ?? string.Empty) : string.Empty;
			floatParameter = animEvent != null ? animEvent.floatParameter : 0f;
			intParameter = animEvent != null ? animEvent.intParameter : 0;
			stringParameter = animEvent != null ? (animEvent.stringParameter ?? string.Empty) : string.Empty;
			objectId = (animEvent != null && animEvent.objectReferenceParameter) ? animEvent.objectReferenceParameter.GetInstanceID() : 0;
		}
	}
}
