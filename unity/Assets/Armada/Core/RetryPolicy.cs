using System;
using System.Net;
using System.Threading.Tasks;

namespace Armada.Client.Core
{
    public static class RetryPolicy
    {
        public static async Task<ApiResponse<T>> ExecuteAsync<T>(Func<Task<ApiResponse<T>>> action, int maxAttempts = 3, int initialDelayMs = 200)
        {
            var attempt = 0;
            var delay = initialDelayMs;

            while (true)
            {
                attempt++;
                var result = await action().ConfigureAwait(false);
                if (result.Success || attempt >= maxAttempts || !IsTransient(result.StatusCode))
                {
                    return result;
                }

                await Task.Delay(delay).ConfigureAwait(false);
                delay *= 2;
            }
        }

        private static bool IsTransient(HttpStatusCode code)
        {
            return code == HttpStatusCode.RequestTimeout ||
                   code == (HttpStatusCode)429 ||
                   (int)code >= 500;
        }
    }
}

