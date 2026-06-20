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
        /// <param name="logger">Логгер в рамках интеграций.</param>
        /// <param name="cacheKey">Ключ кеша экземпляра.</param>
        /// <param name="valueFactory">Функция, используемая для создания значения для кюлча.</param>
        internal static T GetOrAdd(IIntegrationLogger logger, string cacheKey, Func<T> valueFactory)
        {
            if (singletonCollections == null)
            {
                singletonCollections = new TypeCollection<T>();
            }

            T result = singletonCollections.collections.GetOrAdd(cacheKey, (name) =>
            {
                logger.Log("ReceiveAndProcess - TypeCollection - Добавить Публикатор '{0}'", cacheKey);
                return valueFactory();
            });

            return result;
        }
    }
}
