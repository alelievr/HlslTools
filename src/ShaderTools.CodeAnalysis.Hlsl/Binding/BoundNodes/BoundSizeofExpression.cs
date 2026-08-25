using ShaderTools.CodeAnalysis.Hlsl.Symbols;

namespace ShaderTools.CodeAnalysis.Hlsl.Binding.BoundNodes
{
    /// <summary>
    /// sizeof(type) - a compile-time constant of type uint.
    /// </summary>
    internal sealed class BoundSizeofExpression : BoundExpression
    {
        public BoundSizeofExpression(TypeSymbol operandType)
            : base(BoundNodeKind.SizeofExpression)
        {
            OperandType = operandType;
        }

        /// <summary>The type whose size is being queried.</summary>
        public TypeSymbol OperandType { get; }

        public override TypeSymbol Type => IntrinsicTypes.Uint;
    }
}
