﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GRA.Domain.Model;
using GRA.Domain.Report.Abstract;
using GRA.Domain.Report.Attribute;
using GRA.Domain.Repository;
using Microsoft.Extensions.Logging;

namespace GRA.Domain.Report
{
    [ReportInformation(20,
        "Prize Redemption Count Report",
        "Number of times a prize was redeemed by system or branch",
        "Participants")]
    public class PrizeRedemptionCountReport : BaseReport
    {
        private readonly IBranchRepository _branchRepository;
        private readonly IPrizeWinnerRepository _prizeWinnerRepository;
        private readonly ISystemRepository _systemRepository;

        public PrizeRedemptionCountReport(ILogger<PrizeRedemptionCountReport> logger,
            ServiceFacade.Report serviceFacade,
            IBranchRepository branchRepository,
            IPrizeWinnerRepository prizeWinnerRepository,
            ISystemRepository systemRepository) : base(logger, serviceFacade)
        {
            ArgumentNullException.ThrowIfNull(branchRepository);
            ArgumentNullException.ThrowIfNull(prizeWinnerRepository);
            ArgumentNullException.ThrowIfNull(systemRepository);

            _branchRepository = branchRepository;
            _prizeWinnerRepository = prizeWinnerRepository;
            _systemRepository = systemRepository;
        }

        public override async Task ExecuteAsync(ReportRequest request,
            CancellationToken token,
            IProgress<JobStatus> progress = null)
        {
            #region Reporting initialization

            request = await StartRequestAsync(request);

            var criterion = await _serviceFacade.ReportCriterionRepository
                    .GetByIdAsync(request.ReportCriteriaId)
                ?? throw new GraException($"Report criteria {request.ReportCriteriaId} for report request id {request.Id} could not be found.");

            if (criterion.SiteId == null)
            {
                throw new ArgumentException(nameof(criterion.SiteId));
            }

            var report = new StoredReport(_reportInformation.Name,
                _serviceFacade.DateTimeProvider.Now);
            var reportData = new List<object[]>();

            #endregion Reporting initialization

            #region Adjust report criteria as needed

            IEnumerable<int> triggerIds = null;

            if (!string.IsNullOrEmpty(criterion.TriggerList))
            {
                try
                {
                    triggerIds = criterion.TriggerList.Split(',').Select(int.Parse);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex,
                        "Unable to convert trigger id list ({TriggerIdList}) to numbers: {ErrorMessage}",
                        criterion.TriggerList,
                        ex.Message);
                }
            }
            else
            {
                throw new GraException("No prizes selected.");
            }

            #endregion Adjust report criteria as needed

            #region Collect data

            UpdateProgress(progress, 1, "Starting report...", request.Name);

            // header row
            report.HeaderRow = new object[] {
                criterion.SystemId.HasValue ? "Branch Name" : "System Name",
                "# Redeemed"
            };

            int count = 0;
            int totalRedemptionCount = 0;
            if (criterion.SystemId.HasValue)
            {
                var branches = await _branchRepository.GetBySystemAsync(criterion.SystemId.Value);
                foreach (var branch in branches)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    UpdateProgress(progress,
                        ++count * 100 / branches.Count(),
                        $"Processing: {branch.SystemName} - {branch.Name}",
                        request.Name);

                    var redemptionCount = await _prizeWinnerRepository
                        .GetBranchPrizeRedemptionCountAsync(branch.Id, triggerIds);
                    totalRedemptionCount += redemptionCount;

                    IEnumerable<object> row = new object[]
                    {
                        branch.Name,
                        redemptionCount
                    };
                    reportData.Add(row.ToArray());
                }
            }
            else
            {
                var systems = await _systemRepository.GetAllAsync(criterion.SiteId.Value);
                foreach (var system in systems)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    UpdateProgress(progress,
                        ++count * 100 / systems.Count(),
                        $"Processing: {system.Name}",
                        request.Name);

                    var redemptionCount = await _prizeWinnerRepository
                        .GetSystemPrizeRedemptionCountAsync(system.Id, triggerIds);
                    totalRedemptionCount += redemptionCount;

                    IEnumerable<object> row = new object[]
                    {
                        system.Name,
                        redemptionCount
                    };
                    reportData.Add(row.ToArray());
                }
            }

            report.Data = reportData.ToArray();

            IEnumerable<object> footerRow = new object[]
            {
                "Total",
                totalRedemptionCount
            };
            report.FooterRow = footerRow.ToArray();

            #endregion Collect data

            #region Finish up reporting

            if (!token.IsCancellationRequested)
            {
                ReportSet.Reports.Add(report);
            }
            await FinishRequestAsync(request, !token.IsCancellationRequested);

            #endregion Finish up reporting
        }
    }
}
