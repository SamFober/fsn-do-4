namespace WebApi.Models

{
    public class ConcessionItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }

    public class OrderConcessionItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ConcessionItemId { get; set; }
        public int Quantity { get; set; }
        public TicketOrder Order { get; set; }
        public ConcessionItem ConcessionItem { get; set; }
    }
}
