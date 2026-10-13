// <copyright file="Linker_Transaction_LineItem.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class Linker_TransactionLineItem
{
    [Key]
    public int Linker_TransactionLineItemID { get; set; }

    public decimal Discount { get; set; }

    public string DiscountReason { get; set; }

    public decimal FinalSalesPrice { get; set; }

    public decimal LineItemSubtotal { get; set; }

    public int ListingID { get; set; }

    public string Note { get; set; }

    public decimal Quantity { get; set; }

    public decimal Tax { get; set; }

    public int TransactionID { get; set; }

    public Listing Listing { get; set; }
    public Transaction Transaction { get; set; }
}
