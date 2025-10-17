﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using GRA.Abstract;
using GRA.Domain.Model;
using GRA.Domain.Repository;
using GRA.Domain.Service.Abstract;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace GRA.Domain.Service
{
    public class BadgeService : Abstract.BaseUserService<BadgeService>
    {
        public static readonly int DefaultMaxDimension = 400;
        public static readonly int KBSize = 1024;

        private const string BadgePath = "badges";
        private readonly IBadgeRepository _badgeRepository;
        private readonly IPathResolver _pathResolver;
        private readonly SiteLookupService _siteLookupService;

        public BadgeService(ILogger<BadgeService> logger,
            IDateTimeProvider dateTimeProvider,
            IUserContextProvider userContextProvider,
            IBadgeRepository badgeRepository,
            IPathResolver pathResolver,
            SiteLookupService siteLookupService)
            : base(logger, dateTimeProvider, userContextProvider)
        {
            ArgumentNullException.ThrowIfNull(badgeRepository);
            ArgumentNullException.ThrowIfNull(pathResolver);
            ArgumentNullException.ThrowIfNull(siteLookupService);

            _badgeRepository = badgeRepository;
            _pathResolver = pathResolver;
            _siteLookupService = siteLookupService;
        }

        public async Task<Badge> AddBadgeAsync(Badge badge, byte[] imageFile)
        {
            ArgumentNullException.ThrowIfNull(badge);

            badge.SiteId = GetCurrentSiteId();
            var result = await _badgeRepository.AddSaveAsync(GetClaimId(ClaimType.UserId), badge);
            await WriteBadgeFileAsync(result, imageFile, imageType: null);
            result.AltText = badge.AltText?.Trim();
            return await _badgeRepository.UpdateSaveAsync(GetClaimId(ClaimType.UserId), result);
        }

        public async Task<string> GetBadgeFilenameAsync(int id)
        {
            return await _badgeRepository.GetBadgeFileNameAsync(id);
        }

        public static string GetBadgePath(int siteId, int badgeId) =>
            $"site{siteId}/badges/badge{badgeId}.png";


        public async Task<Badge> GetByIdAsync(int badgeId)
        {
            return await _badgeRepository.GetByIdAsync(badgeId);
        }

        public async Task<int> GetCountBySystemAsync(int systemId)
        {
            return await _badgeRepository.GetCountBySystemAsync(systemId);
        }

        public async Task<IEnumerable<string>> GetFilesBySystemAsync(int systemId)
        {
            return await _badgeRepository.GetFilesBySystemAsync(systemId);
        }

        public async Task<IEnumerable<string>> GetNamesAsync(IEnumerable<int> ids)
        {
            return await _badgeRepository.GetBadgeNamesAsync(ids);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
            "CA1308:Normalize strings to uppercase",
            Justification = "Normalize filenames to lowercase")]
        public async Task<Badge> ReplaceBadgeFileAsync(Badge badge,
            byte[] imageFile,
            string uploadFilename)
        {
            ArgumentNullException.ThrowIfNull(badge);

            var existingBadge = await _badgeRepository.GetByIdAsync(badge.Id);

            if (imageFile != null)
            {
                if (File.Exists(existingBadge.Filename))
                {
                    File.Delete(existingBadge.Filename);
                }

                var imageType = ImageType.Png;

                if (!string.IsNullOrEmpty(uploadFilename))
                {
                    var uploadExtension = Path
                        .GetExtension(uploadFilename)
                        .ToLowerInvariant();

                    if (uploadExtension.EndsWith("jpg", StringComparison.OrdinalIgnoreCase)
                        || uploadExtension.EndsWith("jpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        imageType = ImageType.Jpg;
                    }
                }

                await WriteBadgeFileAsync(existingBadge, imageFile, imageType);
            }
            badge.AltText = badge.AltText?.Trim();
            return await _badgeRepository.UpdateSaveAsync(GetClaimId(ClaimType.UserId), badge);
        }

        public async Task ValidateBadgeImageAsync(byte[] badgeImage)
        {
            var (IsSet, SetValue) = await _siteLookupService
                    .GetSiteSettingIntAsync(GetCurrentSiteId(), SiteSettingKey.Badges.MaxFileSize);
            if (IsSet && badgeImage != null && badgeImage.Length > SetValue * KBSize)
            {
                throw new GraException($"File size exceeds the maximum of {SetValue}KB");
            }

            try
            {
                using var image = Image.Load(badgeImage);
                if (image.Height != image.Width)
                {
                    throw new GraException("Image must be square.");
                }
            }
            catch (UnknownImageFormatException uifex)
            {
                throw new GraException("Unknown image type, please upload a JPEG or PNG image.",
                    uifex);
            }
        }

        private string GetFilePath(string filename)
        {
            string contentDir = _pathResolver.ResolveContentFilePath();
            contentDir = Path.Combine(contentDir, $"site{GetCurrentSiteId()}", BadgePath);

            if (!Directory.Exists(contentDir))
            {
                Directory.CreateDirectory(contentDir);
            }
            return Path.Combine(contentDir, filename);
        }

        private string GetUrlPath(string filename)
        {
            return $"site{GetCurrentSiteId()}/{BadgePath}/{filename}";
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
            "CA1308:Normalize strings to uppercase",
            Justification = "Normalize filenames to lowercase")]
        private async Task<string> WriteBadgeFileAsync(Badge badge,
  byte[] imageFile,
  ImageType? imageType)
        {
            const string extension = ".png";
            string filename = $"badge{badge.Id}{extension}";
            string fullFilePath = GetFilePath(filename);

            try
            {
                using var image = Image.Load(imageFile);

                var (IsSet, SetValue) = await _siteLookupService.GetSiteSettingIntAsync(
                  GetCurrentSiteId(),
                  SiteSettingKey.Badges.MaxDimension);

                int maxDimension = IsSet ? SetValue : DefaultMaxDimension;

                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    _logger.LogInformation("Resizing badge file {BadgeFile}", fullFilePath);
                    var sw = Stopwatch.StartNew();

                    image.Mutate(_ => _.Resize(
                      maxDimension,
                      maxDimension,
                      KnownResamplers.Lanczos3));

                    if (image.Metadata != null)
                    {
                        image.Metadata.ExifProfile = null;
                        image.Metadata.IccProfile = null;
                        image.Metadata.IptcProfile = null;
                    }

                    await image.SaveAsPngAsync(fullFilePath, new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.BestCompression,
                        SkipMetadata = true
                    });
                    _logger.LogInformation("Image resize and save of {Filename} took {Elapsed} ms",
                      filename,
                      sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogDebug("Writing out badge file {BadgeFile}", fullFilePath);

                    using var image2 = Image.Load(imageFile);
                    if (image2.Metadata != null)
                    {
                        image2.Metadata.ExifProfile = null;
                        image2.Metadata.IccProfile = null;
                        image2.Metadata.IptcProfile = null;
                    }
                    await image2.SaveAsPngAsync(fullFilePath, new PngEncoder
                    {
                        CompressionLevel = PngCompressionLevel.BestCompression,
                        SkipMetadata = true
                    });
                }
            }
            catch (UnknownImageFormatException uifex)
            {
                _logger.LogError(uifex,
                  "Unknown image format exception on file {Filename}: {ErrorMessage}",
                  filename,
                  uifex.Message);
                throw new GraException("Unknown image type, please upload a JPEG or PNG image.", 
                    uifex);
            }

            return GetUrlPath(filename);
        }
    }
}
