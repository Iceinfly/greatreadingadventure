﻿using System.Collections.Generic;
using System.Threading.Tasks;
using GRA.Domain.Model;
using GRA.Domain.Model.Report;
using GRA.Domain.Model.Utility;

namespace GRA.Domain.Repository
{
    public interface IVendorCodeRepository : IRepository<VendorCode>
    {
        Task<VendorCode> AssignCodeAsync(int vendorCodeTypeId, int userId);

        Task<VendorCode> AssociateCodeAsync(int vendorCodeTypeId,
            int userId,
            string reason,
            int activeUserId);

        Task<ICollection<string>> GetAllCodesAsync(int vendorCodeTypeId);

        Task<IEnumerable<string>> GetAssociatedVendorCodes(int userId);

        Task<VendorCode> GetByCode(string code);

        Task<ICollection<VendorCode>> GetByPackingSlipAsync(string packingSlipNumber);

        Task<ICollection<VendorCode>> GetEarnedCodesAsync(ReportCriterion criterion);

        Task<ICollection<VendorCode>> GetHoldSlipsAsync(string packingSlipNumber);

        Task<ICollection<VendorCodeItemStatus>> GetOrderedNotShipped(int vendorCodeTypeId,
            int? systemId,
            int? branchId);

        Task<ICollection<VendorCode>> GetPendingHouseholdCodes(int headOfHouseholdId);

        Task<IList<ReportVendorCodePending>> GetPendingPrizesPickupBranch();

        Task<ICollection<VendorCode>> GetRemainingPrizesForBranchAsync(int branchId);

        Task<ICollection<VendorCodeItemStatus>> GetShippedNotArrived(int vendorCodeTypeId,
                                    int? systemId,
            int? branchId);

        Task<VendorCodeStatus> GetStatusAsync(int vendorCodeTypeId);

        Task<ICollection<VendorTitlesOnOrder>> GetTitlesOnOrderAsync(int vendorCodeTypeId);

        Task<ICollection<VendorCodeEmailAward>> GetUnreportedEmailAwardCodes(int siteId,
            int vendorCodeTypeId);

        Task<VendorCode> GetUserVendorCode(int userId);

        Task<IEnumerable<VendorCode>> GetUserVendorCodes(int userId);
    }
}
