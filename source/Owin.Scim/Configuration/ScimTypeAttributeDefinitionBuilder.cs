namespace Owin.Scim.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Linq.Expressions;

    using Canonicalization;

    using Extensions;

    public abstract class ScimTypeAttributeDefinitionBuilder<T, TAttribute> : IScimTypeAttributeDefinition
    {
        private readonly ScimTypeDefinitionBuilder<T> _ScimTypeDefinitionBuilder;

        private readonly PropertyDescriptor _PropertyDescriptor;

        private readonly IList<ICanonicalizationRule> _CanonicalizationRules; 

        protected ScimTypeAttributeDefinitionBuilder(
            ScimTypeDefinitionBuilder<T> scimTypeDefinitionBuilder,
            PropertyDescriptor propertyDescriptor)
        {
            _ScimTypeDefinitionBuilder = scimTypeDefinitionBuilder;
            _PropertyDescriptor = propertyDescriptor;
            _CanonicalizationRules = new List<ICanonicalizationRule>();

            // Initialize defaults
            CaseExact = false;
            Mutability = Configuration.Mutability.ReadWrite;
            Required = false;
            Returned = Configuration.Returned.Default;
            Uniqueness = Configuration.Uniqueness.None;

            var descriptionAttr = propertyDescriptor
                .Attributes
                .Cast<Attribute>()
                .SingleOrDefault(attr => attr is DescriptionAttribute) as DescriptionAttribute;

            if (descriptionAttr != null)
            {
                Description = descriptionAttr.Description.RemoveMultipleSpaces();
            }
        }

        public static implicit operator ScimServerConfiguration(ScimTypeAttributeDefinitionBuilder<T, TAttribute> builder)
        {
            return builder._ScimTypeDefinitionBuilder.ScimServerConfiguration;
        }

        public string Description { get; protected set; }

        public Mutability Mutability { get; protected set; }

        public bool Required { get; protected set; }

        public Returned Returned { get; protected set; }

        public Uniqueness Uniqueness { get; protected set; }

        public bool CaseExact { get; protected set; }

        public IEnumerable<ICanonicalizationRule> GetCanonicalizationRules()
        {
            return _CanonicalizationRules;
        }

        public bool MultiValued { get; protected set; }

        protected internal ScimTypeDefinitionBuilder<T> ScimTypeDefinitionBuilder
        {
            get { return _ScimTypeDefinitionBuilder; }
        }

        public PropertyDescriptor AttributeDescriptor
        {
            get { return _PropertyDescriptor; }
        }

        public virtual IScimTypeDefinition TypeDefinitionBuilder { get { return null; } }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> SetDescription(string description)
        {
            Description = description;
            return this;
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> SetMutability(Mutability mutability)
        {
            Mutability = mutability;
            return this;
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> SetRequired(bool required)
        {
            Required = required;
            return this;
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> SetReturned(Returned returned)
        {
            Returned = returned;
            return this;
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> SetUniqueness(Uniqueness uniqueness)
        {
            Uniqueness = uniqueness;
            return this;
        }

        public ScimTypeAttributeDefinitionBuilder<T, TOtherAttribute> For<TOtherAttribute>(
            Expression<Func<T, TOtherAttribute>> attrExp)
        {
            if (attrExp == null) throw new ArgumentNullException("attrExp");

            var memberExpression = attrExp.Body as MemberExpression;
            if (memberExpression == null)
            {
                throw new InvalidOperationException("attrExp must be of type MemberExpression.");
            }

            var propertyDescriptor = TypeDescriptor.GetProperties(typeof (T)).Find(memberExpression.Member.Name, true);
            return (ScimTypeAttributeDefinitionBuilder<T, TOtherAttribute>)ScimTypeDefinitionBuilder.AttributeDefinitions[propertyDescriptor];
        }

        public ScimTypeAttributeDefinitionBuilder<T, TOtherAttribute> For<TOtherAttribute>(
            Expression<Func<T, IEnumerable<TOtherAttribute>>> attrExp)
        {
            if (attrExp == null) throw new ArgumentNullException("attrExp");

            var memberExpression = attrExp.Body as MemberExpression;
            if (memberExpression == null)
            {
                throw new InvalidOperationException("attrExp must be of type MemberExpression.");
            }

            var propertyDescriptor = TypeDescriptor.GetProperties(typeof(T)).Find(memberExpression.Member.Name, true);
            return (ScimTypeAttributeDefinitionBuilder<T, TOtherAttribute>)ScimTypeDefinitionBuilder.AttributeDefinitions[propertyDescriptor];
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> AddCanonicalizationRule(CanonicalizationAction<TAttribute> rule)
        {
            var func = new StatefulCanonicalizationFunc<TAttribute>(
                (TAttribute value, ref object state) =>
                {
                    rule.Invoke(value);
                    return value;
                });

            return AddCanonicalizationRule(func);
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> AddCanonicalizationRule(CanonicalizationFunc<TAttribute> rule)
        {
            var func = new StatefulCanonicalizationFunc<TAttribute>((TAttribute value, ref object state) => rule.Invoke(value));

            return AddCanonicalizationRule(func);
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> AddCanonicalizationRule(StatefulCanonicalizationAction<TAttribute> rule)
        {
            var func = new StatefulCanonicalizationFunc<TAttribute>(
                (TAttribute value, ref object state) =>
                {
                    rule.Invoke(value, ref state);
                    return value;
                });

            return AddCanonicalizationRule(func);
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> AddCanonicalizationRule(StatefulCanonicalizationFunc<TAttribute> rule)
        {
            _CanonicalizationRules.Add(new CanonicalizationRule<TAttribute>(_PropertyDescriptor, rule));
            return this;
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> ClearCanonicalizationRules()
        {
            _CanonicalizationRules.Clear();
            return this;
        }

        public ScimTypeAttributeDefinitionBuilder<T, TAttribute> AddAcceptableValues(params TAttribute[] acceptableValues)
        {
            // TODO: (DG) impl
            return this;
        }
    }
}