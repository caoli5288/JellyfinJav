using AngleSharp;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JellyfinJav.JellyfinJav.Providers.JavBus
{
    public class JavBusExternalId : IExternalId
    {
        public string Name => "JavBus";

        public string Key => "JavBus";

        public string UrlFormatString => "http://javbus.com/cn/{0}";

        public bool Supports(IHasProviderIds item) => item is Movie;
    }

    public class JavBusMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>
    {
        private readonly IServerConfigurationManager configManager;
        private readonly IHttpClient httpClient;

        public string Name => "JavBus";

        public JavBusMetadataProvider(IServerConfigurationManager configManager,
                           IHttpClient httpClient)
        {
            this.configManager = configManager;
            this.httpClient = httpClient;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return httpClient.GetResponse(new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken
            });
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            Console.WriteLine("!!! GetMetadata " + info.ToString());
            if (info.ProviderIds.ContainsKey("JavBus"))
            {
                return GetMovieFromResult(info.Name, await LoadMovie(info.ProviderIds["JavBus"]));
            }

            var results = await GetSearchResults(info, cancellationToken);
            if (results.Count() != 1)
            {
                return new MetadataResult<Movie>();
            }

            return GetMovieFromResult(info.Name, await LoadMovie(results.First().ProviderIds["JavBus"]));
        }

        private MetadataResult<Movie> GetMovieFromResult(String oldName, JavBusResult result)
        {
            return new MetadataResult<Movie>()
            {
                HasMetadata = true,
                Item = new Movie()
                {
                    OriginalTitle = oldName,
                    Name = result.Name,
                    ProviderIds = new Dictionary<string, string>() { { "JavBus", result.GetCode() } },
                    Genres = result.Genres.ToArray()

                },
                People = (from actress in result.Actresses
                          select new PersonInfo()
                          {
                              Name = actress.Name,
                              Type = "JAV Actress",
                              ImageUrl = actress.ImageUrl
                          }).ToList()
            };
        }

        private async Task<JavBusResult> LoadMovie(String code)
        {
            Console.WriteLine("!!! Load movie " + code);
            var res = await httpClient.GetResponse(new HttpRequestOptions
            {
                Url = String.Format("https://www.javbus.com/cn/{0}", code)
            });

            var html = await new StreamReader(res.Content).ReadToEndAsync();
            var doc = await BrowsingContext.New().OpenAsync(req => req.Content(html));

            var image = doc.QuerySelector(".container .screencap img");

            return new JavBusResult
            {
                Url = res.ResponseUrl,
                Name = image.GetAttribute("title"),
                ImageUrl = image.GetAttribute("href"),
                Actresses = from e in doc.QuerySelectorAll(".container .star-box a img")
                            select new Actress()
                            {
                                Name = e.GetAttribute("title"),
                                ImageUrl = e.GetAttribute("src")
                            },
                Genres = from e in doc.QuerySelectorAll(".container .genre a") select e.TextContent
            };
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            return from result in await SearchMovies(searchInfo.Name)
                   select new RemoteSearchResult()
                   {
                       SearchProviderName = "JavBus",
                       Name = result.Name,
                       ImageUrl = result.ImageUrl,
                       ProviderIds = new Dictionary<string, string>() { { "JavBus", result.GetCode() } }
                   };
        }

        private async Task<IEnumerable<JavBusResult>> SearchMovies(String code)
        {
            var res = await httpClient.GetResponse(new HttpRequestOptions
            {
                Url = String.Format("https://www.javbus.com/cn/search/{0}", code)
            });
            var html = await new StreamReader(res.Content).ReadToEndAsync();
            var doc = await BrowsingContext.New().OpenAsync(req => req.Content(html));
            return from element in doc.QuerySelectorAll(".movie-box")
                   select new JavBusResult()
                   {
                       Name = element.QuerySelector("img").GetAttribute("title"),
                       Url = element.GetAttribute("href"),
                       ImageUrl = element.QuerySelector("img").GetAttribute("href")
                   };
        }
    }

    public class JavBusResult
    {
        public String Name { get; set; }

        public String Url { get; set; }

        public String ImageUrl { get; set; }

        public IEnumerable<Actress> Actresses { set; get; }

        public String GetCode() => new Url(Url).Path;

        public IEnumerable<String> Genres { set; get; }
    }

    public class Actress
    {
        public String Name { get; set; }

        public String ImageUrl { get; set; }
    }
}
