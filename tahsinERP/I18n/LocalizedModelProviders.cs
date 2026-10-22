using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;

namespace tahsinERP.I18n
{
    /// <summary>
    /// [Display(Name = "...")] tarjimasi: Name resx kaliti bo'lsa (masalan "Product_Name") — joriy tilda,
    /// aks holda yozilgani o'zgarmaydi (hali tarjima qilinmagan o'zbekcha nomlar ishlayveradi).
    /// Til so'rov bo'yicha almashgani uchun metama'lumot keshlanmaydi (Cached... provayder emas).
    /// </summary>
    public sealed class LocalizedMetadataProvider : DataAnnotationsModelMetadataProvider
    {
        protected override ModelMetadata CreateMetadata(IEnumerable<Attribute> attributes, Type containerType,
            Func<object> modelAccessor, Type modelType, string propertyName)
        {
            var metadata = base.CreateMetadata(attributes, containerType, modelAccessor, modelType, propertyName);
            metadata.DisplayName = TranslateKey(metadata.DisplayName);
            metadata.Watermark = TranslateKey(metadata.Watermark);
            metadata.Description = TranslateKey(metadata.Description);
            metadata.ShortDisplayName = TranslateKey(metadata.ShortDisplayName);
            return metadata;
        }

        internal static string TranslateKey(string value)
        {
            return !string.IsNullOrEmpty(value) && L.Translator.Has(value) ? L.T(value) : value;
        }
    }

    /// <summary>
    /// Validatsiya xabarlari: ErrorMessage resx kaliti bo'lsa — joriy tilda ({0} = maydon nomi, Range uchun
    /// {1}/{2} = chegaralar); ErrorMessage berilmagan bo'lsa — standart inglizcha o'rniga umumiy Val_* matni;
    /// oddiy matn bo'lsa — o'zgarmaydi. Server va mijoz (data-val-*) xabarlari bir xil.
    /// </summary>
    public sealed class LocalizedValidatorProvider : DataAnnotationsModelValidatorProvider
    {
        protected override IEnumerable<ModelValidator> GetValidators(ModelMetadata metadata, ControllerContext context, IEnumerable<Attribute> attributes)
        {
            foreach (var validator in base.GetValidators(metadata, context, attributes))
            {
                var attribute = AttributeOf(validator);
                yield return attribute == null ? validator : new LocalizedValidator(metadata, context, validator, attribute);
            }
        }

        private static readonly PropertyInfo AttributeProperty =
            typeof(DataAnnotationsModelValidator).GetProperty("Attribute", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static ValidationAttribute AttributeOf(ModelValidator validator)
        {
            return validator is DataAnnotationsModelValidator ? AttributeProperty?.GetValue(validator) as ValidationAttribute : null;
        }

        private sealed class LocalizedValidator : ModelValidator
        {
            private readonly ModelValidator _inner;
            private readonly ValidationAttribute _attribute;

            public LocalizedValidator(ModelMetadata metadata, ControllerContext context, ModelValidator inner, ValidationAttribute attribute)
                : base(metadata, context)
            {
                _inner = inner;
                _attribute = attribute;
            }

            public override bool IsRequired => _inner.IsRequired;

            public override IEnumerable<ModelValidationResult> Validate(object container)
            {
                return _inner.Validate(container)
                    .Select(r => new ModelValidationResult { MemberName = r.MemberName, Message = Translate(r.Message) })
                    .ToList();
            }

            public override IEnumerable<ModelClientValidationRule> GetClientValidationRules()
            {
                var rules = _inner.GetClientValidationRules().ToList();
                foreach (var rule in rules)
                    rule.ErrorMessage = Translate(rule.ErrorMessage);
                return rules;
            }

            private string Translate(string message)
            {
                string key = _attribute.ErrorMessage;
                if (string.IsNullOrEmpty(key))
                {
                    if (!string.IsNullOrEmpty(_attribute.ErrorMessageResourceName)) return message;
                    key = DefaultKey(_attribute);
                    if (key == null) return message;
                }
                else if (!L.Translator.Has(key))
                {
                    return message;   // oddiy matn (hali kalitga o'tkazilmagan)
                }
                return L.T(key, Args());
            }

            private object[] Args()
            {
                string name = Metadata.GetDisplayName();
                switch (_attribute)
                {
                    case RangeAttribute r: return new[] { name, r.Minimum, r.Maximum };
                    case StringLengthAttribute s: return new object[] { name, s.MaximumLength, s.MinimumLength };
                    case MaxLengthAttribute m: return new object[] { name, m.Length };
                    case MinLengthAttribute m: return new object[] { name, m.Length };
                    case System.ComponentModel.DataAnnotations.CompareAttribute c: return new object[] { name, LocalizedMetadataProvider.TranslateKey(c.OtherPropertyDisplayName ?? c.OtherProperty) };
                    default: return new object[] { name };
                }
            }

            private static string DefaultKey(ValidationAttribute attribute)
            {
                switch (attribute)
                {
                    case RequiredAttribute _: return "Val_Required";
                    case RangeAttribute _: return "Val_Range";
                    case StringLengthAttribute _: return "Val_MaxLength";
                    case MaxLengthAttribute _: return "Val_MaxLength";
                    case MinLengthAttribute _: return "Val_MinLength";
                    case EmailAddressAttribute _: return "Val_Email";
                    case PhoneAttribute _: return "Val_Phone";
                    case System.ComponentModel.DataAnnotations.CompareAttribute _: return "Val_Compare";
                    case RegularExpressionAttribute _: return "Val_Invalid";
                    default: return null;
                }
            }
        }
    }
}
