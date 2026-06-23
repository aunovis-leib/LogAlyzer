#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LogAnalyzer.Tests
{
    internal static class StaTestHelper
    {
        public static void Run(Action action)
        {
            Exception? ex = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    ex = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (ex is not null)
                throw new AggregateException(ex);
        }

        public static T Run<T>(Func<T> func)
        {
            Exception? ex = null;
            T? result = default;
            var thread = new Thread(() =>
            {
                try
                {
                    result = func();
                }
                catch (Exception e)
                {
                    ex = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (ex is not null)
                throw new AggregateException(ex);
            return result!;
        }

        public static void Run(Func<Task> func)
        {
            Exception? ex = null;
            var thread = new Thread(() =>
            {
                try
                {
                    func().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    ex = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (ex is not null)
                throw new AggregateException(ex);
        }
    }
}
