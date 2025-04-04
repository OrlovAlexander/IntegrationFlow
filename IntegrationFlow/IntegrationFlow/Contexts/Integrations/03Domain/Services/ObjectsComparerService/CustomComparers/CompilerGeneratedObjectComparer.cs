using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Services.ObjectsComparerService.CustomComparers
{
    internal class CompilerGeneratedObjectComparer : AbstractDynamicObjectsComprer<object>
    {
        public CompilerGeneratedObjectComparer(ComparisonSettings settings, BaseComparer parentComparer, IComparersFactory factory)
            : base(settings, parentComparer, factory)
        {
        }

        public override bool IsMatch(Type type, object obj1, object obj2)
        {
            return (obj1 != null || obj2 != null) &&
                   (obj1 == null || obj1.GetType().GetCustomAttributes(typeof(CompilerGeneratedAttribute), true) != null) &&
                   (obj2 == null || obj2.GetType().GetCustomAttributes(typeof(CompilerGeneratedAttribute), true) != null);
        }

        public override bool IsStopComparison(Type type, object obj1, object obj2)
        {
            return true;
        }

        public override bool SkipMember(Type type, MemberInfo member)
        {
            return false;
        }

        protected override IList<string> GetProperties(object obj)
        {
            if (obj == null)
            {
                return new List<string>();
            }

            // Только public и internal свойства
            var props = new List<string>();
            var type = obj.GetType();
            var members = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var propertyInfo in members)
            {
                var getInternal = propertyInfo.GetMethod != null && propertyInfo.GetMethod.IsAssembly;
                var getPublic = propertyInfo.GetMethod != null && propertyInfo.GetMethod.IsPublic;
                var setInternal = propertyInfo.SetMethod != null && propertyInfo.SetMethod.IsAssembly;
                var setPublic = propertyInfo.SetMethod != null && propertyInfo.SetMethod.IsPublic;
                if (getInternal || getPublic || setInternal || setPublic)
                {
                    props.Add(propertyInfo.Name);
                }
            }
            return props.Distinct().ToList();
        }

        protected override bool TryGetMemberValue(object obj, string propertyName, out object value)
        {
            value = null;
            if (obj == null)
            {
                return false;
            }

            var propertyInfo = obj.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (propertyInfo == null)
            {
                return false;
            }

            value = propertyInfo.GetValue(obj, null);

            return true;

        }
    }
}
