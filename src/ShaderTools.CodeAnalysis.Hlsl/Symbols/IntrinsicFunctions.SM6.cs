using System;
using System.Collections.Generic;

namespace ShaderTools.CodeAnalysis.Hlsl.Symbols
{
    partial class IntrinsicFunctions
    {
        public static readonly IEnumerable<FunctionSymbol> AllSM6Functions = CreateSM6Functions();

        private static IEnumerable<FunctionSymbol> CreateSM6Functions()
        {
            var allFunctions = new List<FunctionSymbol>();

            allFunctions.Add(Create0(
                "WaveGetLaneCount",
                "Returns the number of lanes in a wave on this architecture.",
                IntrinsicTypes.Uint));

            allFunctions.Add(Create0(
                "WaveGetLaneIndex",
                "Returns the index of the current lane within the current wave.",
                IntrinsicTypes.Uint));

            allFunctions.Add(Create0(
                "WaveIsFirstLane",
                "Returns true only for the active lane in the current wave with the smallest index.",
                IntrinsicTypes.Bool));

            allFunctions.AddRange(Create1(
                "WaveActiveAnyTrue",
                "Returns true if the expression is true in any of the active lanes in the current wave.",
                new[] { IntrinsicTypes.Bool },
                "expr",
                "The boolean expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveAllTrue",
                "Returns true if the expression is true in all active lanes in the current wave.",
                new[] { IntrinsicTypes.Bool },
                "expr",
                "The boolean expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveBallot",
                "Returns a 4-bit unsigned integer bitmask of the evaluation of the Boolean expression for all active lanes in the specified wave.",
                new[] { IntrinsicTypes.Bool },
                "expr",
                "The boolean expression to evaluate.",
                new[] { IntrinsicTypes.Uint4 }));

            allFunctions.AddRange(Create2(
                "WaveReadLaneAt",
                "Returns the value of the expression for the given lane index within the specified wave.",
                IntrinsicTypes.AllNumericTypes,
                "expr",
                "The expression to evaluate.",
                "laneIndex",
                "The input lane index must be uniform across the wave.",
                overrideParameterType2: IntrinsicTypes.Uint));

            allFunctions.AddRange(Create1(
                "WaveReadLaneFirst",
                "Returns the value of the expression for the active lane of the current wave with the smallest index.",
                IntrinsicTypes.AllNumericTypes,
                "expr",
                "The expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveAllEqual",
                "Returns true if the expression is the same for every active lane in the current wave (and thus uniform across it).",
                IntrinsicTypes.AllNumericTypes,
                "expr",
                "The expression to evaluate.",
                new[] { IntrinsicTypes.Bool }));

            allFunctions.AddRange(Create1(
                "WaveActiveBitAnd",
                "Returns the bitwise AND of all the values of the expression across all active lanes in the current wave and replicates it back to all active lanes.",
                IntrinsicTypes.AllIntTypes,
                "expr",
                "The expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveBitOr",
                "Returns the bitwise OR of all the values of the expression across all active lanes in the current wave and replicates it back to all active lanes.",
                IntrinsicTypes.AllIntTypes,
                "expr",
                "The expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveBitXor",
                "Returns the bitwise XOR of all the values of the expression across all active lanes in the current wave and replicates it back to all active lanes.",
                IntrinsicTypes.AllIntTypes,
                "expr",
                "The expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveCountBits",
                "Counts the number of boolean variables which evaluate to true across all active lanes in the current wave, and replicates the result to all lanes in the wave.",
                new[] { IntrinsicTypes.Bool },
                "bBit",
                "The boolean variables to evaluate. Providing an explicit true Boolean value returns the number of active lanes.",
                new[] { IntrinsicTypes.Uint }));

            allFunctions.AddRange(Create1(
                "WaveActiveMax",
                "Returns the maximum value of the expression across all active lanes in the current wave and replicates it back to all active lanes.",
                IntrinsicTypes.AllNumericTypes,
                "expr",
                "The expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveMin",
                "Returns the minimum value of the expression across all active lanes in the current wave and replicates it back to all active lanes.",
                IntrinsicTypes.AllNumericTypes,
                "expr",
                "The expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveProduct",
                "Multiplies the values of the expression together across all active lanes in the current wave and replicates it back to all active lanes.",
                IntrinsicTypes.AllNumericTypes,
                "expr",
                "The expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WaveActiveSum",
                "Sums up the value of the expression across all active lanes in the current wave and replicates it to all lanes in the current wave.",
                IntrinsicTypes.AllNumericTypes,
                "expr",
                "The expression to evaluate."));

            allFunctions.AddRange(Create1(
                "WavePrefixCountBits",
                "Returns the sum of all the specified boolean variables set to true across all active lanes with indices smaller than the current lane.",
                new[] { IntrinsicTypes.Bool },
                "bBit",
                "The specified boolean variables.",
                new[] { IntrinsicTypes.Uint }));

            allFunctions.AddRange(Create1(
                "WavePrefixSum",
                "Returns the sum of all of the values in the active lanes with smaller indices than this one.",
                IntrinsicTypes.AllNumericTypes,
                "value",
                "The value to sum up."));

            allFunctions.AddRange(Create1(
                "WavePrefixProduct",
                "Returns the product of all of the values in the active lanes in this wave with indices less than this lane.",
                IntrinsicTypes.AllNumericTypes,
                "value",
                "The value to multiply."));

            allFunctions.AddRange(Create1(
                "WaveMatch",
                "Returns the set of lanes in the current wave that have the same value as the given expression, as a 128-bit mask packed into a uint4. (Requires shader model 6.5.)",
                IntrinsicTypes.AllNumericTypes,
                "value",
                "The value to compare across the wave.",
                new[] { IntrinsicTypes.Uint4 }));

            allFunctions.AddRange(Create2(
                "WaveMultiPrefixSum",
                "Returns the sum of all of the values in the active lanes, in the group identified by the mask, with indices smaller than the current lane. (Requires shader model 6.5.)",
                IntrinsicTypes.AllNumericTypes,
                "value",
                "The value to sum up.",
                "mask",
                "A uint4 bitmask that partitions the active lanes into disjoint groups; the prefix operation is evaluated independently within each group.",
                overrideParameterType2: IntrinsicTypes.Uint4));

            allFunctions.AddRange(Create2(
                "WaveMultiPrefixProduct",
                "Returns the product of all of the values in the active lanes, in the group identified by the mask, with indices smaller than the current lane. (Requires shader model 6.5.)",
                IntrinsicTypes.AllNumericTypes,
                "value",
                "The value to multiply.",
                "mask",
                "A uint4 bitmask that partitions the active lanes into disjoint groups; the prefix operation is evaluated independently within each group.",
                overrideParameterType2: IntrinsicTypes.Uint4));

            allFunctions.AddRange(Create2(
                "WaveMultiPrefixBitAnd",
                "Returns the bitwise AND of all of the values in the active lanes, in the group identified by the mask, with indices smaller than the current lane. (Requires shader model 6.5.)",
                IntrinsicTypes.AllIntTypes,
                "value",
                "The value to combine.",
                "mask",
                "A uint4 bitmask that partitions the active lanes into disjoint groups; the prefix operation is evaluated independently within each group.",
                overrideParameterType2: IntrinsicTypes.Uint4));

            allFunctions.AddRange(Create2(
                "WaveMultiPrefixBitOr",
                "Returns the bitwise OR of all of the values in the active lanes, in the group identified by the mask, with indices smaller than the current lane. (Requires shader model 6.5.)",
                IntrinsicTypes.AllIntTypes,
                "value",
                "The value to combine.",
                "mask",
                "A uint4 bitmask that partitions the active lanes into disjoint groups; the prefix operation is evaluated independently within each group.",
                overrideParameterType2: IntrinsicTypes.Uint4));

            allFunctions.AddRange(Create2(
                "WaveMultiPrefixBitXor",
                "Returns the bitwise XOR of all of the values in the active lanes, in the group identified by the mask, with indices smaller than the current lane. (Requires shader model 6.5.)",
                IntrinsicTypes.AllIntTypes,
                "value",
                "The value to combine.",
                "mask",
                "A uint4 bitmask that partitions the active lanes into disjoint groups; the prefix operation is evaluated independently within each group.",
                overrideParameterType2: IntrinsicTypes.Uint4));

            allFunctions.AddRange(Create2(
                "WaveMultiPrefixCountBits",
                "Returns the count of the boolean variables which evaluate to true, across all active lanes in the group identified by the mask, with indices smaller than the current lane. (Requires shader model 6.5.)",
                new[] { IntrinsicTypes.Bool },
                "val",
                "The boolean value to count.",
                "mask",
                "A uint4 bitmask that partitions the active lanes into disjoint groups; the prefix operation is evaluated independently within each group.",
                new[] { IntrinsicTypes.Uint },
                overrideParameterType2: IntrinsicTypes.Uint4));

            allFunctions.AddRange(Create2(
                "QuadReadLaneAt",
                "Returns the specified source value from the lane identified by the lane ID within the current quad.",
                IntrinsicTypes.AllNumericTypes,
                "sourceValue",
                "The requested type.",
                "quadLaneID",
                "The lane ID; this will be a value from 0 to 3.",
                overrideParameterType2: IntrinsicTypes.Uint));

            allFunctions.AddRange(Create1(
                "QuadReadAcrossDiagonal",
                "Returns the specified local value which is read from the diagonally opposite lane in this quad.",
                IntrinsicTypes.AllNumericTypes,
                "localValue",
                "The requested type."));

            allFunctions.AddRange(Create1(
                "QuadReadAcrossX",
                "Returns the specified local value read from the other lane in this quad in the X direction.",
                IntrinsicTypes.AllNumericTypes,
                "localValue",
                "The requested type."));

            allFunctions.AddRange(Create1(
                "QuadReadAcrossY",
                "Returns the specified local value read from the other lane in this quad in the Y direction.",
                IntrinsicTypes.AllNumericTypes,
                "localValue",
                "The requested type."));

            // DXR Intrinsic Functions

            allFunctions.Add(Create0(
                "AcceptHitAndEndSearch",
                "Used in an any hit shader to commit the current hit and then stop searching for more hits for the ray.",
                IntrinsicTypes.Void));

            allFunctions.Add(new FunctionSymbol(
                "CallShader",
                "Invokes another shader from within a shader.",
                null,
                IntrinsicTypes.Void,
                f => new[]
                {
                    new ParameterSymbol("ShaderIndex", "An unsigned integer representing the index into the callable shader table specified in the call to DispatchRays.", f, IntrinsicTypes.Uint),
                    new ParameterSymbol("Parameter", "The user-defined parameters to pass to the callable shader. This parameter structure must match the parameter structure used in the callable shader pointed to in the shader table.", f, IntrinsicTypes.Template, ParameterDirection.Inout)
                }));

            allFunctions.Add(Create0(
                "IgnoreHit",
                "Called from an any hit shader to reject the hit and end the shader.",
                IntrinsicTypes.Void));

            allFunctions.Add(new FunctionSymbol(
                "ReportHit",
                "Called by an intersection shader to report a ray intersection.",
                null,
                IntrinsicTypes.Bool,
                f => new[]
                {
                    new ParameterSymbol("THit", "A float value specifying the parametric distance of the intersection.", f, IntrinsicTypes.Float),
                    new ParameterSymbol("HitKind", "An unsigned integer that identifies the type of hit that occurred. This is a user-specified value in the range of 0-127. The value can be read by any hit or closest hit shaders with the HitKind intrinsic.", f, IntrinsicTypes.Uint),
                    new ParameterSymbol("Attributes", "The user-defined Intersection Attribute Structure specifying the intersection attributes.", f, IntrinsicTypes.Template)
                }));

            allFunctions.Add(new FunctionSymbol(
                "TraceRay",
                "Sends a ray into a search for hits in an acceleration structure.",
                null,
                IntrinsicTypes.Void,
                f => new[]
                {
                    new ParameterSymbol("AccelerationStructure", "The top-level acceleration structure to use. Specifying a NULL acceleration structure forces a miss.", f, IntrinsicTypes.RaytracingAccelerationStructure),
                    new ParameterSymbol("RayFlags", "Valid combination of ray_flag values. Only defined ray flags are propagated by the system, i.e. are visible to the RayFlags shader intrinsic.", f, IntrinsicTypes.Uint),
                    new ParameterSymbol("InstanceInclusionMask", "An unsigned integer, the bottom 8 bits of which are used to include or reject geometry instances based on the InstanceMask in each instance.", f, IntrinsicTypes.Uint),
                    new ParameterSymbol("RayContributionToHitGroupIndex", "An unsigned integer specifying the offset to add into addressing calculations within shader tables for hit group indexing. Only the bottom 4 bits of this value are used.", f, IntrinsicTypes.Uint),
                    new ParameterSymbol("MultiplierForGeometryContributionToShaderIndex", "An unsigned integer specifying the stride to multiply by GeometryContributionToHitGroupIndex, which is just the 0 based index the geometry was supplied by the app into its bottom-level acceleration structure. Only the bottom 16 bits of this multiplier value are used.", f, IntrinsicTypes.Uint),
                    new ParameterSymbol("MissShaderIndex", "An unsigned integer specifying the index of the miss shader within a shader table.", f, IntrinsicTypes.Uint),
                    new ParameterSymbol("Ray", "A RayDesc representing the ray to be traced.", f, IntrinsicTypes.RayDesc),
                    new ParameterSymbol("Payload", "A user defined ray payload accessed both for input and output by shaders invoked during raytracing. After TraceRay completes, the caller can access the payload as well.", f, IntrinsicTypes.Template, ParameterDirection.Inout)
                }));

            // DXR System Value Intrinsics

            allFunctions.Add(Create0(
                "DispatchRaysIndex",
                "Gets the current location within the width, height and depth obtained with the DispatchRaysDimensions system value intrinsic.",
                IntrinsicTypes.Uint3));

            allFunctions.Add(Create0(
                "DispatchRaysDimensions",
                "The width, height and depth values from the D3D12_DISPATCH_RAYS_DESC structure specified in the originating DispatchRays call.",
                IntrinsicTypes.Uint3));

            allFunctions.Add(Create0(
                "WorldRayOrigin",
                "The world-space origin of the current ray.",
                IntrinsicTypes.Float3));

            allFunctions.Add(Create0(
                "WorldRayDirection",
                "The world-space direction for the current ray.",
                IntrinsicTypes.Float3));

            allFunctions.Add(Create0(
                "RayTMin",
                "A float representing the current parametric starting point for the ray.",
                IntrinsicTypes.Float));
            
            allFunctions.Add(Create0(
                "RayTCurrent",
                "A float representing the current parametric ending point for the ray.",
                IntrinsicTypes.Float));

            allFunctions.Add(Create0(
                "RayFlags",
                "An unsigned integer containing the current ray_flag flags.",
                IntrinsicTypes.Uint));
            
            allFunctions.Add(Create0(
                "InstanceIndex",
                "The autogenerated index of the current instance in the top-level raytracing acceleration structure.",
                IntrinsicTypes.Uint));

            allFunctions.Add(Create0(
                "InstanceID",
                "The user-provided identifier for the instance on the bottom-level acceleration structure instance within the top-level structure.",
                IntrinsicTypes.Uint));

            allFunctions.Add(Create0(
                "PrimitiveIndex",
                "Retrieves the autogenerated index of the primitive within the geometry inside the bottom-level acceleration structure instance.",
                IntrinsicTypes.Uint));

            allFunctions.Add(Create0(
                "ObjectRayOrigin",
                "The object-space origin for the current ray. Object-space refers to the space of the current bottom-level acceleration structure.",
                IntrinsicTypes.Float3));

            allFunctions.Add(Create0(
                "ObjectRayDirection",
                "The object-space direction for the current ray. Object-space refers to the space of the current bottom-level acceleration structure.",
                IntrinsicTypes.Float3));

            allFunctions.Add(Create0(
                "ObjectToWorld3x4",
                "A matrix for transforming from object-space to world-space. Object-space refers to the space of the current bottom-level acceleration structure.",
                IntrinsicTypes.GetMatrixType(ScalarType.Float, 3, 4)));

            allFunctions.Add(Create0(
                "ObjectToWorld4x3",
                "A matrix for transforming from object-space to world-space. Object-space refers to the space of the current bottom-level acceleration structure.",
                IntrinsicTypes.GetMatrixType(ScalarType.Float, 4, 3)));

            allFunctions.Add(Create0(
                "WorldToObject3x4",
                "A matrix for transforming from world-space to object-space. Object-space refers to the space of the current bottom-level acceleration structure.",
                IntrinsicTypes.GetMatrixType(ScalarType.Float, 3, 4)));

            allFunctions.Add(Create0(
                "WorldToObject4x3",
                "A matrix for transforming from world-space to object-space. Object-space refers to the space of the current bottom-level acceleration structure.",
                IntrinsicTypes.GetMatrixType(ScalarType.Float, 4, 3)));

            allFunctions.Add(Create0(
                "HitKind",
                "Returns the value passed as the HitKind parameter to ReportHit.",
                IntrinsicTypes.Uint));

            allFunctions.AddRange(Create2(
                "SetMeshOutputCounts",
                "At the beginning of the shader the implementation internally sets a count of vertices and primitives to be exported from a threadgroup to 0. It means that if a mesh shader returns without calling this function, it will not output any mesh. This function sets the actual number of outputs from the threadgroup.",
                new[] { IntrinsicTypes.Uint },
                "numVertices", "The number of vertices.",
                "numPrimitives", "The number of primitives.",
                new[] { IntrinsicTypes.Void }));

            // HLSL 2021 'select' intrinsic - a function form of the ternary operator that
            // operates component-wise. The condition is a bool of the same shape (scalar /
            // vector / matrix) as the values being selected.
            var selectValueTypes = IntrinsicTypes.AllNumericTypes;
            var selectConditionTypes = new TypeSymbol[selectValueTypes.Length];
            for (var i = 0; i < selectValueTypes.Length; i++)
                selectConditionTypes[i] = GetMatchingBoolType(selectValueTypes[i]);

            allFunctions.AddRange(Create3(
                "select",
                "Selects between two values on a per-component basis, based on a boolean condition. Introduced in HLSL 2021 as a function form of the ternary '?:' operator.",
                selectValueTypes,
                "condition", "The boolean condition. When true, trueValue is returned; otherwise falseValue is returned.",
                "trueValue", "The value returned when the condition is true.",
                "falseValue", "The value returned when the condition is false.",
                overrideParameterTypes1: selectConditionTypes));

            return allFunctions;
        }

        /// <summary>
        /// Returns the bool intrinsic type with the same shape (scalar / vector / matrix) as
        /// the given numeric type.
        /// </summary>
        private static TypeSymbol GetMatchingBoolType(TypeSymbol type)
        {
            switch (type)
            {
                case IntrinsicVectorTypeSymbol v:
                    return IntrinsicTypes.GetVectorType(ScalarType.Bool, v.NumComponents);
                case IntrinsicMatrixTypeSymbol m:
                    return IntrinsicTypes.GetMatrixType(ScalarType.Bool, m.Rows, m.Cols);
                default:
                    return IntrinsicTypes.GetScalarType(ScalarType.Bool);
            }
        }
    }
}
