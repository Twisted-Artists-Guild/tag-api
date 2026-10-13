using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TAGWEBAPI.Models;

public class ListingSalesItem
{
    [Key]
    public int ListingSalesItemNum { get; set; }

    public int? InventoryRemaining { get; set; }

    public int? QuantitySold { get; set; }

    [ForeignKey("DigitalDeliverySpecs")]
    public int? DigitalDeliverySpecsID { get; set; }

    public DigitalDeliverySpecs DigitalDeliverySpecs { get; set; }

    [ForeignKey("ShippingSpecs")]
    public int? ShippingSpecsID { get; set; }

    public ShippingSpecs ShippingSpecs { get; set; }

    [ForeignKey("TicketType")]
    public int? TicketTypeID { get; set; }

    public TicketType TicketType { get; set; }

    [ForeignKey("Listing")]
    public int ListingID { get; set; }

    public Listing Listing { get; set; }
}
