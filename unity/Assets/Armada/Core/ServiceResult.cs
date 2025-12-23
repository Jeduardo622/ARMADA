using System.Net;

namespace Armada.Client.Core
{
    public sealed class ServiceResult<T>
    {
        public T Data { get; init; }
        public bool Success { get; init; }
        public HttpStatusCode Status { get; init; }
        public string ErrorReason { get; init; }
        public bool FeatureDisabled { get; init; }

        public static ServiceResult<T> FromResponse(ApiResponse<T> response, bool featureDisabled = false)
        {
            return new ServiceResult<T>
            {
                Data = response.Data,
                Success = response.Success,
                Status = response.StatusCode,
                ErrorReason = response.ErrorReason,
                FeatureDisabled = featureDisabled
            };
        }
    }
}

