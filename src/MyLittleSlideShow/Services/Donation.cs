using System.Net;

namespace MyLittleSlideShow.Services;

/// <summary>Ersetzt ZZZ_PayPal.cs. Das Oeffnen des Browsers liegt jetzt im ShellService.</summary>
public static class Donation
{
    public enum Language { De, En }

    private const string Business = "thomas.mueller@tuta.io";

    public static string BuildPayPalUrl(Language language)
    {
        var (description, country, currency) = language switch
        {
            Language.De => ("Vielen Dank fuer Ihre Spende. Diese traegt dazu bei, weitere " +
                            "kostenfreie Programme zu entwickeln und zur Verfuegung zu stellen.",
                            "DE", "EUR"),
            _ => ("Thank you for your donation. This helps to develop more programs " +
                  "and share them for free.", "US", "USD")
        };

        return "https://www.paypal.com/cgi-bin/webscr"
               + "?cmd=_donations"
               + "&business=" + WebUtility.UrlEncode(Business)
               + "&lc=" + country
               + "&item_name=" + WebUtility.UrlEncode(description)
               + "&currency_code=" + currency
               + "&bn=PP%2dDonationsBF";
    }
}
