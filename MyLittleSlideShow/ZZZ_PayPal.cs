using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZZZ
{
    class PayPalSpende
    {

       
        public enum Sprache
        {
            DE,
            EN
        }
        static public void PayPalSpenden(Sprache welcheSprache)
        {
            string url = "";
            string business = "mail.t.mueller@googlemail.com";         // your paypal email
            string description = string.Empty;                         // '%20' represents a space. remember HTML!
            string country = string.Empty;                             // DE, AU, US, etc.
            string currency = string.Empty;                            // EUR, AUD, USD, etc.


            switch (welcheSprache)
            {
                case Sprache.DE:
                    description = "Vielen Dank fuer Ihre Spende. Diese traegt dazu bei, weitere kostenfreie Programme zu Entwickeln und zur Verfuegung stellen zu koennen.";           
                    country = "DE";
                    currency = "EUR";
                    break;

                case Sprache.EN:
                    description = "Thank you for your donation. This Helps to develope more programs and share them for free.";
                    country = "US";
                    currency = "USD";
                    break;
            }

            url += "https://www.paypal.com/cgi-bin/webscr" +
                "?cmd=" + "_donations" +
                "&business=" + business +
                "&lc=" + country +
                "&item_name=" + description +
                "&currency_code=" + currency +
                "&bn=" + "PP%2dDonationsBF";

            System.Diagnostics.Process.Start(url);
        }
    }
}
