﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace AzureSearch.Linq.Utlities
{
    static class AsyncHelper
    {
        public static T RunSync<T>(Func<Task<T>> action)
        {
            return SynchronizationContext.Current == null
                ? action().GetAwaiter().GetResult()
                : Task.Run(async () => await action()).GetAwaiter().GetResult();
        }
    }
}
