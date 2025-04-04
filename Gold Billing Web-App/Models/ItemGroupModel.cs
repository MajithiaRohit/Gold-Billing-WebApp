﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gold_Billing_Web_App.Models
{
    public class ItemGroupModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Item Group Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Item Group Name must be between 2 and 100 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s-&/]+$", ErrorMessage = "Item Group Name can only contain letters, numbers, spaces, hyphens, and ampersands")]
        public string? Name { get; set; }

        public DateTime Date { get; set; }

        public int UserId { get; set; }

        [NotMapped]
        public UserAccountModel User { get; set; }
    }
}