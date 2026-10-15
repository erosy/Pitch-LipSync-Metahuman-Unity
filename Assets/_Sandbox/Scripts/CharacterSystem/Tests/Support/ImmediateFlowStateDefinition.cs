namespace Sandbox.CharacterSystem.Tests
{
    /// <summary>Proves a new state behavior needs no controller branches.</summary>
    public sealed class ImmediateFlowStateDefinition : CharacterFlowStateDefinition
    {
        public override CharacterFlowState CreateState(CharacterFlowContext context) => new State();
        sealed class State : CharacterFlowState
        {
            public override bool Enter(out string error) { error = null; return true; }
            public override CharacterFlowStateResult Tick(float deltaTime, out string error)
            { error = null; return CharacterFlowStateResult.Completed; }
        }
    }
}
