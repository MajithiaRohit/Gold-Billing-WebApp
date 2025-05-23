﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gold_Billing_Web_App.Models.ViewModels
{
    public class TransactionViewModel
    {
        [Required]
        [Display(Name = "Bill Number")]
        public string? BillNo { get; set; }

        [Required(ErrorMessage = "Date is required.")]
        public DateTime Date { get; set; }

        public string? Narration { get; set; }

        [Required(ErrorMessage = "Transaction Type is required.")]
        public string TransactionType { get; set; } = "";

        [Required(ErrorMessage = "At least one item is required.")]
        [MinLength(1, ErrorMessage = "At least one item is required.")]
        public List<TransactionModel> Items { get; set; } = new List<TransactionModel>();

        [NotMapped]
        public AccountModel? Account { get; set; }

        [NotMapped]
        public ItemModel? Item { get; set; }

        public int UserId { get; set; }
    }
}