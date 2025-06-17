using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

namespace YifySubtitles
{
    public class YifySubtitlesSubtitleProvider : ISubtitleProvider, IHasOrder
    {

        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IApplicationHost _appHost;
        private readonly string baseUrl = "https://yifysubtitles.ch";

        public YifySubtitlesSubtitleProvider(ILogger logger, IHttpClient httpClient, IApplicationHost appHost)
        {
            _logger = logger;
            _httpClient = httpClient;
            _appHost = appHost;
        }

        private HttpRequestOptions BaseRequestOptions => new HttpRequestOptions
        {
            UserAgent = $"Emby/{_appHost.ApplicationVersion}"
        };
        public string Name => "Yify Subtitles";

        public IEnumerable<VideoContentType> SupportedMediaTypes => new List<VideoContentType> { VideoContentType.Movie };

        public int Order => 2;

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var subtitleResponse = new SubtitleResponse();

            var opts = BaseRequestOptions;
            opts.CancellationToken = cancellationToken;
            opts.Url = $"{baseUrl}/subtitle/{id}.zip";

            using (var response = await _httpClient.GetResponse(opts).ConfigureAwait(false))
            {
                var archive = new ZipArchive(response.Content);

                var entry = archive.Entries.FirstOrDefault();

                var fileExt = entry.FullName.Split('.').LastOrDefault();

                var lang = id.Split('-').Reverse().ElementAtOrDefault(2);

                var stream = entry.Open();

                subtitleResponse.Stream = stream;
                subtitleResponse.Format = fileExt;
                subtitleResponse.Language = lang;

            }
            return subtitleResponse;
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            var remoteSubtitleInfos = new List<RemoteSubtitleInfo>();
            if (request.IsForced.HasValue || request.IsPerfectMatch)
            {
                return remoteSubtitleInfos;
            }

            var imdb = request.GetProviderId("imdb");
            if (string.IsNullOrEmpty(imdb))
            {
                return remoteSubtitleInfos;
            }

            var opts = BaseRequestOptions;
            opts.CancellationToken = cancellationToken;
            opts.Url = $"{baseUrl}/movie-imdb/{imdb}";
            using (var response = await _httpClient.GetResponse(opts).ConfigureAwait(false))
            {
                var htmlDocument = new HtmlDocument();
                htmlDocument.Load(response.Content);
                var nodes = htmlDocument.DocumentNode.SelectNodes("//table[@class='table other-subs']/tbody/tr");
                if (nodes == null)
                {
                    return remoteSubtitleInfos;
                }
                foreach (var node in nodes)
                {
                    var language = node.SelectSingleNode("td[2]/span[@class='sub-lang']");
                    if (!request.LanguageInfo.ContainsLanguage(language?.InnerText))
                    {
                        continue;
                    }

                    var remoteSubtitleInfo = new RemoteSubtitleInfo
                    {
                        ProviderName = Name,
                        Format = "srt",
                        Language = language?.InnerText
                    };

                    var rating = node.SelectSingleNode("td[1]");
                    remoteSubtitleInfo.CommunityRating = float.Parse(rating?.InnerText);

                    var name = node.SelectSingleNode("td[3]/a[@href]");
                    remoteSubtitleInfo.Name = name?.InnerText.Replace("subtitle ", "").Split('\n').FirstOrDefault();

                    var id = name?.GetAttributeValue("href", "").Split('/').LastOrDefault();
                    remoteSubtitleInfo.Id = id;

                    var hearing = node.SelectSingleNode("td[4]/span[@title='hearing impaired']");
                    remoteSubtitleInfo.IsHearingImpaired = hearing != null;

                    var uploader = node.SelectSingleNode("td[5]");
                    remoteSubtitleInfo.Author = uploader?.InnerText;

                    remoteSubtitleInfos.Add(remoteSubtitleInfo);
                }
            }
            return remoteSubtitleInfos;
        }
    }
}
