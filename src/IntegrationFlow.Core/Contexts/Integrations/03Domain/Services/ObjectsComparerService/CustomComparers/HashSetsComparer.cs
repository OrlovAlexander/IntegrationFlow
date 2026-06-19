using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IntegrationFlow.Contexts.Integrations._03Domain.Services.ObjectsComparerService.Utils;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Services.ObjectsComparerService.CustomComparers
{
    internal class HashSetsComparer : AbstractEnumerablesComparer
    {
        public HashSetsComparer(ComparisonSettings settings, BaseComparer parentComparer, IComparersFactory factory)
            : base(settings, parentComparer, factory)
        {
        }

        public override IEnumerable<Difference> CalculateDifferences(Type type, object obj1, object obj2)
        {
            if (obj1 == null && obj2 == null)
            {
                yield break;
            }

            var typeInfo = (obj1 ?? obj2).GetType();

            Type elementType = typeInfo.GetInterfaces()
                    .Where(
                        i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(ISet<>))
                    .Select(i => i.GetGenericArguments()[0])
                    .First();

            var enumerablesComparerType = typeof(HashSetsComparer<>).MakeGenericType(elementType);
            var comparer = (IComparer)Activator.CreateInstance(enumerablesComparerType, Settings, this, Factory);

            foreach (var difference in comparer.CalculateDifferences(type, obj1, obj2))
            {
                yield return difference;
            }
        }

        public override bool IsMatch(Type type, object obj1, object obj2)
        {
            return type.InheritsFrom(typeof(HashSet<>));
        }

        public override bool SkipMember(Type type, MemberInfo member)
        {
            return base.SkipMember(type, member) ||
                   member.Name == PropertyHelper.GetMemberInfo(() => new HashSet<string>().Comparer).Name ||
                   member.Name == PropertyHelper.GetMemberInfo(() => new HashSet<string>().Count).Name;
        }
    }
}
