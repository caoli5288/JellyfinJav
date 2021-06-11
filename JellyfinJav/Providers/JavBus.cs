﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace JellyfinJav.JellyfinJav.Providers
{
    public class JavBus
    {
        public static string Name => "JavBus";

        public static string AsFullUrl(string url) {
            if (url.StartsWith("/")) {
                return "https://javbus.com" + url;
            }
            return url;
        }

        public static async Task<IEnumerable<JavBusResult>> GetResults(HttpClient httpClient, ILogger logger, string name, bool uncensored)
        {
            logger.LogInformation($"Jav find movies {name}");
            try
            {
                var contents = await httpClient.GetStringAsync(uncensored ? $"https://www.javbus.com/uncensored/search/{name}" : $"https://www.javbus.com/search/{name}");
                var doc = await BrowsingContext.New().OpenAsync(req => req.Content(contents));

                var ret = from element in doc.QuerySelectorAll(".movie-box")
                          select new JavBusResult
                          {
                              Name = element.QuerySelector("img").GetAttribute("title"),
                              Url = element.GetAttribute("href"),
                              ImageUrl = AsFullUrl(element.QuerySelector("img").GetAttribute("src")),
                              Code = GetCodeFromUrl(element.GetAttribute("href")).ToUpper(),
                              ReleaseDate = DateTime.Parse(element.QuerySelectorAll("date")[1].TextContent)
                          };

                return ret;
            }
            catch (Exception e)
            {
                if (uncensored)
                {
                    logger.LogInformation($"Jav Search movies {name} exceptions. {e.Message}");
                    return new JavBusResult[0];
                }
                else
                {
                    return await GetResults(httpClient, logger, name, true);
                }
            }
        }

        private static string GetCodeFromUrl(string url)
        {
            var parts = url.Split('/');
            return parts.Last();
        }

        public static async Task<JavBusResult> GetResult(HttpClient httpClient, ILogger logger, string code)
        {
            logger.LogInformation("Jav Load movie " + code);

            var url = $"https://www.javbus.com/{code}";
            var html = await httpClient.GetStringAsync(url);
            var doc = await BrowsingContext.New().OpenAsync(req => req.Content(html));

            var dateStr = doc.QuerySelectorAll(".container .info p")[1].TextContent.Split(' ').Last();
            var image = doc.QuerySelector(".container .screencap img");

            var ret = new JavBusResult
            {
                Code = code.ToUpper(),
                Url = url,
                Name = image.GetAttribute("title"),
                ImageUrl = AsFullUrl(image.GetAttribute("src")),
                Actresses = from e in doc.QuerySelectorAll(".container .star-box a img")
                            select new Actress
                            {
                                Name = e.GetAttribute("title"),
                                ImageUrl = AsFullUrl(e.GetAttribute("src"))
                            },
                Genres = from e in doc.QuerySelectorAll(".container .genre a") select e.TextContent,
                ReleaseDate = DateTime.Parse(dateStr),
                Screenshots = from e in doc.QuerySelectorAll(".container .sample-box") select e.GetAttribute("href")
            };

            return ret;
        }

        public static MetadataResult<Movie> GetMovieFromResult(JavBusResult result)
        {
            return new MetadataResult<Movie>
            {
                HasMetadata = true,
                Item = new Movie
                {
                    OriginalTitle = $"{result.Code} {string.Join(" ", result.Genres)}",
                    Name = result.Name,
                    ProviderIds = new Dictionary<string, string> { { "JavBus", result.Code } },
                    Genres = result.Genres.ToArray(),
                    PremiereDate = result.ReleaseDate,
                    ProductionYear = result.ReleaseDate.Year
                },
                People = new List<PersonInfo>(from actress in result.Actresses
                                              select new PersonInfo
                                              {
                                                  Name = actress.Name,
                                                  Type = "JAV Actress",
                                                  ImageUrl = actress.ImageUrl
                                              })
            };
        }
    }

    public class JavBusExternalId : IExternalId
    {
        public string Name => "JavBus";

        public string Key => "JavBus";

        public string UrlFormatString => "http://javbus.com/{0}";

        public string ProviderName => "JavBus";

        public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie;
        }
    }

    public class JavBusImageProvider : IRemoteImageProvider
    {
        private readonly HttpClient httpClient;
        private readonly ILogger logger;

        public JavBusImageProvider(IHttpClientFactory cf, ILoggerFactory loggerFactory)
        {
            this.httpClient = cf.CreateClient();
            this.logger = loggerFactory.CreateLogger("JavBus");
        }

        public bool Supports(BaseItem item)
        {
            return item is Movie;
        }

        public string Name => JavBus.Name;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary, ImageType.Screenshot, ImageType.Thumb };
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Jav Get image for {item.Name}");

            var code = item.GetProviderId(Name);
            if (string.IsNullOrEmpty(code))
            {
                return new RemoteImageInfo[0];
            }

            var r = await JavBus.GetResult(httpClient, logger, code);

            var thumb = new List<RemoteImageInfo>
            {
                new RemoteImageInfo
                {
                    Type = ImageType.Thumb,
                    Url = r.ImageUrl,
                    ProviderName = Name
                }
            };

            var primary =
                from e in await JavBus.GetResults(httpClient, logger, code, false)
                where e.Code.Equals(code)
                select new RemoteImageInfo
                {
                    Type = ImageType.Primary,
                    Url = e.ImageUrl,
                    ProviderName = Name
                };

            var screenshots =
                from url in r.Screenshots
                select new RemoteImageInfo
                {
                    Type = ImageType.Screenshot,
                    Url = url,
                    ProviderName = Name
                };

            return thumb.Concat(primary).Concat(screenshots);
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return httpClient.GetAsync(url, cancellationToken);
        }
    }

    public class JavBusMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>
    {
        private readonly HttpClient httpClient;
        private readonly ILogger logger;

        public JavBusMetadataProvider(IHttpClientFactory cf, ILoggerFactory loggerFactory)
        {
            this.httpClient = cf.CreateClient();
            this.logger = loggerFactory.CreateLogger("JavBusMetadata");
        }

        public string Name => "JavBus";

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return httpClient.GetAsync(url, cancellationToken);
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            if (info.ProviderIds.ContainsKey("JavBus"))
            {
                logger.LogInformation("Jav Get metadata with code " + info.ProviderIds["JavBus"]);
                return JavBus.GetMovieFromResult(await JavBus.GetResult(httpClient, logger, info.ProviderIds["JavBus"]));
            }

            logger.LogInformation($"Jav Get metadata with name {info.Name}");

            var results = await GetSearchResults(info, cancellationToken);
            try
            {
                var first = results.First();
                return JavBus.GetMovieFromResult(await JavBus.GetResult(httpClient, logger, first.ProviderIds["JavBus"]));
            }
            catch (Exception e)
            {
                logger.LogInformation($"Jav Get metadata no result for {info.Name}, {e.Message}");
                return new MetadataResult<Movie>();
            }
        }


        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo,
            CancellationToken cancellationToken)
        {
            var code = searchInfo.GetProviderId(JavBus.Name);

            if (string.IsNullOrEmpty(code))
            {
                logger.LogInformation($"Jav search {searchInfo.Name}");

                code = new Regex("[A-Za-z]+-[0-9]+").Match(searchInfo.Name).Value;
                if (string.IsNullOrEmpty(code))
                {
                    code = searchInfo.Name.Split(' ').First();
                }

                return from e in await JavBus.GetResults(httpClient, logger, code, false)
                       select new RemoteSearchResult
                       {
                           SearchProviderName = "JavBus",
                           Name = e.Name,
                           ImageUrl = e.ImageUrl,
                           ProviderIds = new Dictionary<string, string> { { "JavBus", e.Code } },
                           PremiereDate = e.ReleaseDate,
                           ProductionYear = e.ReleaseDate.Year
                       };
            }

            var result = await JavBus.GetResult(httpClient, logger, searchInfo.ProviderIds["JavBus"]);
            return new[]
            {
                new RemoteSearchResult
                {
                    SearchProviderName = "JavBus",
                    Name = result.Name,
                    ImageUrl = result.ImageUrl,
                    ProviderIds = new Dictionary<string, string> {{"JavBus", result.Code}},
                    PremiereDate = result.ReleaseDate,
                    ProductionYear = result.ReleaseDate.Year
                }
            };
        }
    }

    public class JavBusResult
    {
        public string Code { get; set; }

        public string Name { get; set; }

        public string Url { get; set; }

        public string ImageUrl { get; set; }

        public IEnumerable<Actress> Actresses { set; get; }

        public IEnumerable<string> Genres { set; get; }

        public IEnumerable<string> Screenshots { set; get; }

        public DateTime ReleaseDate { get; set; }
    }

    public class Actress
    {
        public string Name { get; set; }

        public string ImageUrl { get; set; }
    }
}
