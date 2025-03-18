using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebApi.Interfaces.Services;
using WebApi.Models;

namespace WebApi.Services
{
    public class TicketPdfServiceQuestPdf : ITicketPdfService
    {
        private readonly byte[] headerImageBytes;
        private readonly QRCodeGenerator qRCodeGenerator;

        public TicketPdfServiceQuestPdf()
        {
            headerImageBytes = File.ReadAllBytes("Resources/Images/Cinemagia_logo.jpg");
            this.qRCodeGenerator = new QRCodeGenerator();
        }
        public byte[] CreatePdfTicketsAsByteArray(List<Ticket> tickets, Guid orderToken)
        {
            var document = CreateTicketDocument(tickets, orderToken);
            return document.GeneratePdf();
        }

        private IDocument CreateTicketDocument(List<Ticket> tickets, Guid orderToken)
        {
            var qrCodeData = qRCodeGenerator.CreateQrCode(orderToken.ToString(), QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            return Document.Create(container =>
            {
                foreach (var ticket in tickets)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(50);

                        page.Header().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item()
                                .PaddingBottom(5)
                                .Text("Ticket")
                                .FontSize(20).SemiBold();

                                column.Item()
                                .PaddingBottom(5)
                                .Text(text =>
                                {
                                    text.Span("Naam: " + ticket.CustomerName).SemiBold();
                                });

                                column.Item()
                                .PaddingBottom(5)
                                .Text(text =>
                                {
                                    text.Span("Email: " + ticket.CustomerEmail).SemiBold();
                                });
                            });

                            row.ConstantItem(100).Image(headerImageBytes);
                        });

                        page.Content()
                        .AlignCenter()
                        .PaddingTop(10)
                        .Column(column =>
                        {
                            column.Spacing(20);

                            column.Item()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("Film");
                                    header.Cell().Element(CellStyle).Text("Datum");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Zaal");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Rij");
                                    header.Cell().Element(CellStyle).AlignRight().Text("Stoelnummer");

                                    static IContainer CellStyle(IContainer container)
                                    {
                                        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                                    }
                                });

                                table.Cell().Element(CellStyle).Text(ticket.Presentation.Movie.Title);
                                table.Cell().Element(CellStyle).Text(ticket.Presentation.StartTime.ToString());
                                table.Cell().Element(CellStyle).AlignRight().Text(ticket.Presentation.Hall.Name);
                                table.Cell().Element(CellStyle).AlignRight().Text(ticket.Seat.RowNumber.ToString());
                                table.Cell().Element(CellStyle).AlignRight().Text(ticket.Seat.SeatNumber.ToString());

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                                }
                            });

                            column.Item()
                            .AlignCenter()
                            .MaxWidth(2, Unit.Inch)
                            .Image(qrCode.GetGraphic(20));
                        });

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                }
            });
        }
    }
}
