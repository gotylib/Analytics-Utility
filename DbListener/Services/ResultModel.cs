namespace DbListener.Services
{
    public class ResultModel<T, E>
    {
        private ResultModel() { }
        private ResultModel(bool result) : this(result, default, default) { }

        private ResultModel(bool result, E error) : this(result, error, default) { }

        private ResultModel(bool result, T value) : this(result, default, value) { }

        private ResultModel(bool result, E? error, T? value)
        {
            IsSuccessful = result;
            Error = error;
            Value = value;
        }

        /// <summary>
        /// Признак успешности получения результата
        /// </summary>
        public bool IsSuccessful { get; private set; }

        /// <summary>
        /// Объект ошибки
        /// </summary>
        public E? Error { get; private set; }

        /// <summary>
        /// Результат операции
        /// </summary>
        public T? Value { get; private set; }

        /// <summary>
        /// Создать модель с успешным результатом
        /// </summary>
        /// <returns>Модель с успешным результатом</returns>
        public static ResultModel<T, E> CreateSuccessfulResult(T value)
        {
            return new ResultModel<T, E>(true, value);
        }

        /// <summary>
        /// Создать модель с успешным результатом
        /// </summary>
        /// <returns>Модель с успешным результатом</returns>
        public static ResultModel<T, E> CreateSuccessfulResult()
        {
            return new ResultModel<T, E>(true);
        }

        /// <summary>
        /// Cоздать модель с ошибкой
        /// </summary>
        /// <returns>Модель с ошибкой</returns>
        public static ResultModel<T, E> CreateFailedResult(E error)
        {
            return new ResultModel<T, E>(false, error);
        }

        public static ResultModel<T, E> CreateFailedResult()
        {
            return new ResultModel<T, E>(false);
        }
    }
}
