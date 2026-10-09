namespace ShaderTools.CodeAnalysis.Hlsl.Binding.BoundNodes
{
    internal sealed class BoundPayloadAccessQualifier : BoundVariableQualifier
    {
        public BoundPayloadAccessQualifier()
            : base(BoundNodeKind.PayloadAccessQualifier)
        {

        }
    }
}
