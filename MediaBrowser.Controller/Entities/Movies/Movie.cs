﻿using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Entities.Movies
{
    /// <summary>
    /// Class Movie
    /// </summary>
    public class Movie : Video, IHasCriticRating, IHasSoundtracks, IHasBudget, IHasKeywords, IHasTrailers, IHasThemeMedia, IHasTaglines, IHasTags, IHasPreferredMetadataLanguage, IHasAwards, IHasMetascore, IHasLookupInfo<MovieInfo>
    {
        public List<Guid> SpecialFeatureIds { get; set; }

        public List<Guid> SoundtrackIds { get; set; }

        public List<Guid> ThemeSongIds { get; set; }
        public List<Guid> ThemeVideoIds { get; set; }

        /// <summary>
        /// Gets or sets the preferred metadata country code.
        /// </summary>
        /// <value>The preferred metadata country code.</value>
        public string PreferredMetadataCountryCode { get; set; }

        public string PreferredMetadataLanguage { get; set; }

        public Movie()
        {
            SpecialFeatureIds = new List<Guid>();
            SoundtrackIds = new List<Guid>();
            RemoteTrailers = new List<MediaUrl>();
            LocalTrailerIds = new List<Guid>();
            ThemeSongIds = new List<Guid>();
            ThemeVideoIds = new List<Guid>();
            Taglines = new List<string>();
            Tags = new List<string>();
            Keywords = new List<string>();
        }

        public string AwardSummary { get; set; }

        public float? Metascore { get; set; }

        public List<Guid> LocalTrailerIds { get; set; }
        public List<string> Keywords { get; set; }

        public List<MediaUrl> RemoteTrailers { get; set; }

        /// <summary>
        /// Gets or sets the tags.
        /// </summary>
        /// <value>The tags.</value>
        public List<string> Tags { get; set; }

        /// <summary>
        /// Gets or sets the taglines.
        /// </summary>
        /// <value>The taglines.</value>
        public List<string> Taglines { get; set; }

        /// <summary>
        /// Gets or sets the budget.
        /// </summary>
        /// <value>The budget.</value>
        public double? Budget { get; set; }

        /// <summary>
        /// Gets or sets the revenue.
        /// </summary>
        /// <value>The revenue.</value>
        public double? Revenue { get; set; }

        /// <summary>
        /// Gets or sets the critic rating.
        /// </summary>
        /// <value>The critic rating.</value>
        public float? CriticRating { get; set; }

        /// <summary>
        /// Gets or sets the critic rating summary.
        /// </summary>
        /// <value>The critic rating summary.</value>
        public string CriticRatingSummary { get; set; }

        /// <summary>
        /// Gets or sets the name of the TMDB collection.
        /// </summary>
        /// <value>The name of the TMDB collection.</value>
        public string TmdbCollectionName { get; set; }

        /// <summary>
        /// Gets the user data key.
        /// </summary>
        /// <returns>System.String.</returns>
        public override string GetUserDataKey()
        {
            return this.GetProviderId(MetadataProviders.Tmdb) ?? this.GetProviderId(MetadataProviders.Imdb) ?? base.GetUserDataKey();
        }

        protected override async Task BeforeRefreshMetadata(MetadataRefreshOptions options, List<FileSystemInfo> fileSystemChildren, CancellationToken cancellationToken)
        {
            await base.BeforeRefreshMetadata(options, fileSystemChildren, cancellationToken).ConfigureAwait(false);

            // Must have a parent to have special features
            // In other words, it must be part of the Parent/Child tree
            if (LocationType == LocationType.FileSystem && Parent != null && !IsInMixedFolder)
            {
                var specialFeaturesChanged = await RefreshSpecialFeatures(options, fileSystemChildren, cancellationToken).ConfigureAwait(false);

                if (specialFeaturesChanged)
                {
                    options.ForceSave = true;
                }
            }
        }

        private async Task<bool> RefreshSpecialFeatures(MetadataRefreshOptions options, List<FileSystemInfo> fileSystemChildren, CancellationToken cancellationToken)
        {
            var newItems = LoadSpecialFeatures(fileSystemChildren).ToList();
            var newItemIds = newItems.Select(i => i.Id).ToList();

            var itemsChanged = !SpecialFeatureIds.SequenceEqual(newItemIds);

            var tasks = newItems.Select(i => i.RefreshMetadata(options, cancellationToken));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            SpecialFeatureIds = newItemIds;

            return itemsChanged;
        }

        /// <summary>
        /// Loads the special features.
        /// </summary>
        /// <returns>IEnumerable{Video}.</returns>
        private IEnumerable<Video> LoadSpecialFeatures(IEnumerable<FileSystemInfo> fileSystemChildren)
        {
            var files = fileSystemChildren.OfType<DirectoryInfo>()
                .Where(i => string.Equals(i.Name, "extras", StringComparison.OrdinalIgnoreCase) || string.Equals(i.Name, "specials", StringComparison.OrdinalIgnoreCase))
                .SelectMany(i => i.EnumerateFiles("*", SearchOption.TopDirectoryOnly));

            return LibraryManager.ResolvePaths<Video>(files, null).Select(video =>
            {
                // Try to retrieve it from the db. If we don't find it, use the resolved version
                var dbItem = LibraryManager.GetItemById(video.Id) as Video;

                if (dbItem != null)
                {
                    video = dbItem;
                }

                return video;

                // Sort them so that the list can be easily compared for changes
            }).OrderBy(i => i.Path).ToList();
        }

        protected override bool GetBlockUnratedValue(UserConfiguration config)
        {
            return config.BlockUnratedMovies;
        }

        public MovieInfo GetLookupInfo()
        {
            return GetItemLookupInfo<MovieInfo>();
        }
    }
}
