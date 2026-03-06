
using CNSDemo.Models;

namespace CNSDemo
{
    public interface ICadNotificationApi
    {
        Task<bool> Subscribe(string accessToken, ResponseType responseType);
        Task<List<GetSubscriptionResponse>> GetSubscriptions(string accessToken);
        Task<bool> Resubscribe(string accessToken, string subscriberId);
        Task<string> HealthCheck(string accessToken);
        Task<bool> Unsubscribe(string accessToken, string subscriberId);
    }
}