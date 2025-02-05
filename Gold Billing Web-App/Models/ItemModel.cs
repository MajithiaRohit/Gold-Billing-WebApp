using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class ItemModel
    {
        [Required]
        public string ItemName { get; set; }

        public string ItemGroup { get; set; }

        public string StockMethod { get; set; }

        public bool UsedInStudding { get; set; }

        public decimal ALPercentage { get; set; }

        public bool MaintainPcStock { get; set; }

        public bool LessDetails { get; set; }

        // Restrictions
        public bool BeedsLess { get; set; }
        public bool TagStockLess { get; set; }
        public bool BarsList { get; set; }
        public bool Charges { get; set; }
        public bool Size { get; set; }
        public bool GrossWeight { get; set; }
        public bool Tunch { get; set; }
        public bool Wastage { get; set; }
        public bool RateTunch { get; set; }
        public bool FineAmount { get; set; }
        public bool HMNo { get; set; }
        public bool Stamp { get; set; }
        public bool Batch { get; set; }
        public bool OthersInSale { get; set; }
        public bool WastageLbr { get; set; }
        public bool Pc { get; set; }
        public bool SaleCostrate { get; set; }

        public string AddGwt { get; set; }

        public bool UseStamp { get; set; }
        public bool DiaStonesStudded { get; set; }
        public bool DisplayItemInSale { get; set; }
        public bool DisplayItemInPurchase { get; set; }
        public bool DisplayItemInKarigarEntry { get; set; }
        public bool DisplayItemInStockReports { get; set; }
        public bool PartyDiamondStone { get; set; }
        public bool DontPrintInStudded { get; set; }
        public bool DontLessInStudded { get; set; }
    }
}
