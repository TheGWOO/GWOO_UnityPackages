namespace GWOO.Editor.Tools
{
    internal readonly struct MaterialActionDryRunSummary
    {
        public MaterialActionDryRunSummary(int targetMaterialsCount, int revertableOverridesCount, int reparentCandidatesCount)
        {
            TargetMaterialsCount = targetMaterialsCount;
            RevertableOverridesCount = revertableOverridesCount;
            ReparentCandidatesCount = reparentCandidatesCount;
        }

        public int TargetMaterialsCount { get; }
        public int RevertableOverridesCount { get; }
        public int ReparentCandidatesCount { get; }
    }

    internal readonly struct MaterialMutationResult
    {
        public MaterialMutationResult(int reparentedCount, int revertedOverrideCount)
        {
            ReparentedCount = reparentedCount;
            RevertedOverrideCount = revertedOverrideCount;
        }

        public int ReparentedCount { get; }
        public int RevertedOverrideCount { get; }
    }
}
