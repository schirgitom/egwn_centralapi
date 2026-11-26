using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EGWNCentral.Core.Domain.Entities;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace EGWNCentral.Api.Services;

public sealed class RegistrationDocumentService
{
    private const string FontFamily = "EGWN Lato";
    private const string PortalUrl = "https://portal.wirteilenenergie.at";
    private const double Margin = 42;
    private static readonly XColor Blue = XColor.FromArgb(0, 87, 164);
    private static readonly XColor Dark = XColor.FromArgb(30, 48, 65);
    private static readonly XColor Muted = XColor.FromArgb(87, 106, 123);

    static RegistrationDocumentService()
    {
        GlobalFontSettings.FontResolver = new EmbeddedFontResolver();
    }

    public byte[] Create(Customer customer)
    {
        using var document = new PdfDocument();
        document.Info.Title = "Ihre Registrierung";
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        using (var graphics = XGraphics.FromPdfPage(page))
        using (var logoStream = OpenResource("logo.png"))
        using (var logo = XImage.FromStream(logoStream))
        {
            var width = page.Width.Point;
            var contentHeight = Render(graphics, logo, customer, page, 1, false);
            var scale = Math.Min(1, (page.Height.Point - 2 * Margin) / contentHeight);
            graphics.TranslateTransform((width - width * scale) / 2, Margin);
            graphics.ScaleTransform(scale);
            Render(graphics, logo, customer, page, scale, true);
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static double Render(XGraphics graphics, XImage logo, Customer customer, PdfPage page, double scale, bool draw)
    {
        var width = page.Width.Point;
        var regular = new XFont(FontFamily, 10);
        var small = new XFont(FontFamily, 9);
        var bold = new XFont(FontFamily, 10, XFontStyleEx.Bold);
        var title = new XFont(FontFamily, 20, XFontStyleEx.Bold);
        var codeFont = new XFont(FontFamily, 25, XFontStyleEx.Bold);
        var contentWidth = width - 2 * Margin;
        var layout = new DocumentLayout(graphics, contentWidth, draw);
        var darkBrush = new XSolidBrush(Dark);
        var mutedBrush = new XSolidBrush(Muted);
        var blueBrush = new XSolidBrush(Blue);

        const double logoWidth = 150;
        var logoHeight = logoWidth * logo.PixelHeight / logo.PixelWidth;
        if (draw)
        {
            graphics.DrawImage(logo, (width - logoWidth) / 2, 0, logoWidth, logoHeight);
        }
        layout.Space(logoHeight + 20);
        layout.Write("Willkommen bei der EEG", title, darkBrush, 25);
        layout.Write("Ihre persönlichen Registrierungsdaten", regular, mutedBrush, 17);
        layout.Write("Portal: ", regular, darkBrush, 14);
        var linkTop = layout.Y;
        layout.Write(PortalUrl, regular, blueBrush, 14);
        if (draw)
        {
            var left = (width - width * scale) / 2 + Margin * scale;
            var top = Margin + linkTop * scale;
            var bottom = Margin + layout.Y * scale;
            var right = left + graphics.MeasureString(PortalUrl, regular).Width * scale;
            page.AddWebLink(new PdfRectangle(new XPoint(left, page.Height.Point - bottom),
                new XPoint(right, page.Height.Point - top)), PortalUrl);
        }
        layout.Space(7);

        if (draw)
        {
            graphics.DrawRoundedRectangle(new XSolidBrush(XColor.FromArgb(238, 246, 252)),
                Margin, layout.Y, contentWidth, 90, 10, 10);
        }
        layout.Space(18);
        layout.Write("IHR REGISTRIERUNGSCODE", bold, blueBrush, 18, 15);
        layout.Write(customer.RegistrationCode, codeFont, darkBrush, 29, 15);
        layout.Write("Geben Sie diesen Code bei der Registrierung im Feld „Registrierungscode“ ein.",
            regular, mutedBrush, 14, 15);
        layout.Space(15);

        layout.Write("Ihre Kundendaten", bold, blueBrush, 17);
        layout.Write($"Name: {customer.Name}", regular, darkBrush, 14);
        layout.Write($"Kundennummer: {customer.CustomerNumber}", regular, darkBrush, 14);
        layout.Space(8);

        var meters = customer.MeteringPoints.Where(meter => !meter.IsDeleted)
            .OrderBy(meter => meter.MeteringPointCode, StringComparer.Ordinal)
            .ThenBy(meter => meter.Id)
            .ToArray();
        if (meters.Length > 0)
        {
            layout.Write("Ihre Zählpunkte", bold, blueBrush, 17);
            foreach (var meter in meters)
            {
                var direction = meter.EnergyDirection switch
                {
                    "consumption" => "Bezug",
                    "generation" => "Einspeisung",
                    _ => meter.EnergyDirection
                };
                layout.Write($"{meter.MeteringPointCode} — {direction}", bold, darkBrush, 14);
                if (!string.IsNullOrWhiteSpace(meter.Description))
                {
                    layout.Write($"Bezeichnung: {meter.Description}", regular, mutedBrush, 14, 10);
                }

                var participations = meter.CommunityParticipations
                    .Where(participation => !participation.IsDeleted && participation.EnergyCommunity is { IsDeleted: false })
                    .OrderBy(participation => participation.EnergyCommunity.Name, StringComparer.Ordinal)
                    .ThenBy(participation => participation.EnergyCommunityId)
                    .ToArray();
                if (participations.Length == 0)
                {
                    layout.Write("Energiegemeinschaft: keine zugeordnet", regular, mutedBrush, 14, 10);
                }
                else
                {
                    foreach (var participation in participations)
                    {
                        var percentage = participation.ParticipationPercentage.ToString("0.##", CultureInfo.GetCultureInfo("de-AT"));
                        layout.Write($"{participation.EnergyCommunity.Name} ({percentage} % Teilnahme)", regular, mutedBrush, 14, 10);
                    }
                }
                layout.Space(5);
            }
        }

        layout.Space(8);
        layout.Write("Bitte behandeln Sie Ihren Registrierungscode vertraulich. Bei Ausstellung eines neuen Codes verliert dieser Ausdruck seine Gültigkeit.",
            small, mutedBrush, 13);
        layout.Space(6);
        var date = DateTime.UtcNow.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        layout.Write($"Erstellt am {date} (UTC)", small, mutedBrush, 13);
        return layout.Y + 4;
    }

    private sealed class DocumentLayout(XGraphics graphics, double width, bool draw)
    {
        public double Y { get; private set; }

        public void Space(double height) => Y += height;

        public void Write(string text, XFont font, XBrush brush, double lineHeight, double indent = 0)
        {
            foreach (var line in WrapText(graphics, text, font, width - indent))
            {
                Y += lineHeight;
                if (draw)
                {
                    graphics.DrawString(line, font, brush, Margin + indent, Y - 3);
                }
            }
        }
    }

    private static IReadOnlyCollection<string> WrapText(XGraphics graphics, string text, XFont font, double width)
    {
        text = Regex.Replace(text, @"[\s\p{Cc}]+", " ").Trim();
        var lines = new List<string>();
        var line = new StringBuilder();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (line.Length > 0 && graphics.MeasureString(line.ToString() + element, font).Width > width)
            {
                var currentLine = line.ToString();
                var breakAt = currentLine.LastIndexOf(' ');
                var displayed = breakAt > 0 ? currentLine[..breakAt] : currentLine;
                var remainder = breakAt > 0 ? currentLine[(breakAt + 1)..] : string.Empty;
                lines.Add(displayed);
                line.Clear();
                line.Append(remainder);
            }
            line.Append(element);
        }
        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }
        return lines;
    }

    private static Stream OpenResource(string name)
    {
        var assembly = typeof(RegistrationDocumentService).Assembly;
        return assembly.GetManifestResourceStream($"EGWNCentral.Api.Assets.{name}")
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
    }

    private sealed class EmbeddedFontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            if (!familyName.Equals(FontFamily, StringComparison.OrdinalIgnoreCase) || isItalic)
            {
                return null;
            }
            return new FontResolverInfo(isBold ? "Lato-Bold" : "Lato-Regular");
        }

        public byte[] GetFont(string faceName)
        {
            using var stream = OpenResource(faceName + ".ttf");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
