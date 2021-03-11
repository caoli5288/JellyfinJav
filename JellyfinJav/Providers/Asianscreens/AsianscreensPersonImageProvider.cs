using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System;
using System.Net.Http;

namespace JellyfinJav.Providers.Asianscreens
{
    public class AsianscreensPersonImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory httpClients;

        public string Name => "Asianscreens";

        public AsianscreensPersonImageProvider(IHttpClientFactory httpClients)
        {
            this.httpClients = httpClients;
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancelToken)
        {
            var result = new List<RemoteImageInfo>();

            var id = item.GetProviderId("Asianscreens");
            if (string.IsNullOrEmpty(id))
            {
                return Task.FromResult<IEnumerable<RemoteImageInfo>>(result);
            }

            result.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Type = ImageType.Primary,
                Url = AsianscreensApi.getCover(id)
            });

            return Task.FromResult<IEnumerable<RemoteImageInfo>>(result);
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancelToken)
        {
            using (var httpClient = httpClients.CreateClient()) {
                return httpClient.GetAsync(url, cancelToken);
            }
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        public bool Supports(BaseItem item)
        {
            return item is Person;
        }
    }
}