using System;
using System.Collections.Concurrent;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    /// <summary>
    /// Коллекция типов
    /// </summary>
    /// <typeparam name="T">Типаж</typeparam>
    internal class TypeCollection<T>
    {
        private ConcurrentDictionary<string, T> collections { get; set; }

        private static TypeCollection<T> singletonCollections;

        /// <summary>
        /// Ctor
        /// </summary>
        internal TypeCollection()
        {
            collections = new ConcurrentDictionary<string, T>();
        }

        /// <summary>
        /// Добавить в коллекцию используя указанную функцию, если ключ еще не существует.
        /// Возвращает новое значение или существующенее значение, если ключ существует.
        /// </summary>
        /// <param name="valueFactory">Функция, используемая для создания значения для кюлча.</param>
        internal static T GetOrAdd(ILogger logger, Func<T> valueFactory)
        {
            if (singletonCollections == null)
            {
                singletonCollections = new TypeCollection<T>();
            }

            var assemblyQualifiedName = typeof(T).AssemblyQualifiedName;

            T result = singletonCollections.collections.GetOrAdd(assemblyQualifiedName, (name) => 
            {
                logger.Log("ReceiveAndProcess - TypeCollection - Добавить Публикатор '{0}'", assemblyQualifiedName);
                return valueFactory();
            });

            // logger.Log("Найден Публикатор типа '{0}' по полному имени типа '{1}'", typeof(T).AssemblyQualifiedName, assemblyQualifiedName);

            return result;
        }
    }
}
