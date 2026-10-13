// <copyright file="Linker_TicketToEvent.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
public class Linker_TicketToEvent
{
    [Key]
    public int Linker_TicketToEventID { get; set; }

    public int EventID { get; set; }

    public int TicketID { get; set; }

    public int TransactionID { get; set; }

    public  Event Event { get; set; }
    public Ticket Ticket { get; set; }
    public Transaction Transaction { get; set; }
}
