﻿using System;

namespace Cauldron.Interception.Cecilator.Coders
{
    public sealed class BooleanExpressionArgCoder :
        BooleanExpressionCoderBase<BooleanExpressionArgCoder, BooleanExpressionCoder>,
        ICallMethod<BooleanExpressionCallCoder>,
        IFieldOperations<BooleanExpressionFieldCoder>,
        IBinaryOperators<BooleanExpressionArgCoder>,
        ICasting<BooleanExpressionArgCoder>
    {
        internal BooleanExpressionArgCoder(BooleanExpressionCoderBase coder, BuilderType builderType) : base(coder, builderType)
        {
        }

        public override BooleanExpressionCoder End => new BooleanExpressionCoder(this);

        public static implicit operator InstructionBlock(BooleanExpressionArgCoder coder) => coder.instructions;

        #region Call Methods

        public BooleanExpressionCallCoder Call(Method method)
        {
            this.InternalCall(null, method);
            return new BooleanExpressionCallCoder(this, method.ReturnType);
        }

        public BooleanExpressionCallCoder Call(Method method, params object[] parameters)
        {
            this.InternalCall(null, method, parameters);
            return new BooleanExpressionCallCoder(this, method.ReturnType);
        }

        public BooleanExpressionCallCoder Call(Method method, params Func<Coder, object>[] parameters)
        {
            this.InternalCall(null, method, this.CreateParameters(parameters));
            return new BooleanExpressionCallCoder(this, method.ReturnType);
        }

        #endregion Call Methods

        #region Field Operations

        public BooleanExpressionFieldCoder Load(Field field)
        {
            InstructionBlock.CreateCodeForFieldReference(this, field.FieldType, field, false);
            return new BooleanExpressionFieldCoder(this, field.FieldType);
        }

        public BooleanExpressionFieldCoder Load(Func<BuilderType, Field> field) => Load(field(this.builderType));

        public Coder SetValue(Field field, object value)
        {
            this.instructions.Append(InstructionBlock.SetValue(this, null, field, value));
            return new Coder(this);
        }

        public Coder SetValue(Field field, Func<Coder, object> value) => SetValue(field, value(this.NewCoder()));

        public Coder SetValue(Func<BuilderType, Field> field, object value) => this.SetValue(field(this.instructions.associatedMethod.type), value);

        public Coder SetValue(Func<BuilderType, Field> field, Func<Coder, object> value) => SetValue(field, value(this.NewCoder()));

        #endregion Field Operations

        #region Casting Operations

        public BooleanExpressionArgCoder As(BuilderType type)
        {
            InstructionBlock.CastOrBoxValues(this, type);
            return new BooleanExpressionArgCoder(this, type);
        }

        CoderBase ICasting.As(BuilderType type) => this.As(type);

        #endregion Casting Operations

        #region Binary Operators

        public BooleanExpressionArgCoder And(Func<Coder, object> other)
        {
            this.And(this.builderType, other);
            return this;
        }

        public BooleanExpressionArgCoder And(Field field)
        {
            this.And(this.builderType, field);
            return this;
        }

        public BooleanExpressionArgCoder And(LocalVariable variable)
        {
            this.And(this.builderType, variable);
            return this;
        }

        public BooleanExpressionArgCoder And(ParametersCodeBlock arg)
        {
            this.And(this.builderType, arg);
            return this;
        }

        public BooleanExpressionArgCoder Invert()
        {
            this.InvertInternal();
            return this;
        }

        public BooleanExpressionArgCoder Or(Field field)
        {
            this.Or(this.builderType, field);
            return this;
        }

        public BooleanExpressionArgCoder Or(LocalVariable variable)
        {
            this.Or(this.builderType, variable);
            return this;
        }

        public BooleanExpressionArgCoder Or(Func<Coder, object> other)
        {
            this.Or(this.builderType, other);
            return this;
        }

        public BooleanExpressionArgCoder Or(ParametersCodeBlock arg)
        {
            this.Or(this.builderType, arg);
            return this;
        }

        #endregion Binary Operators
    }
}