using LogiusDigipoort.ServiceReferenceAanleveren;
using LogiusDigipoort.ServiceReferenceStatusInformatie;
using LogiusDigipoort.WusChannel;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Threading;

namespace LogiusDigipoort
{
    class Program
    {
        /* ---------------------------------------------------------------------
         * Digipoort migratie-instructie voor het WUS-koppelvlak
         * https://aansluiten.procesinfrastructuur.nl/site/documentatie/digipoort-migratie-instructie-voor-het-wus-koppelvlak
         */

        // locatie en wachtwoord van uw PKI overheids certificaat
        // (voor zowel pre- als productie omgeving heeft u voor de vernieuwde digipoort vanaf 2026 een 
        const string CLIENT_CERTIFICATE = @"[bestandslocatie]";
        const string CLIENT_CERTIFICATE_PASSWORD = "voorbeeldwachtwoord12345";

        // locatie van het wus (pre)productie service certificaat
        const string SERVER_CERTIFICATE = @"C:\temp\digipoort\wus.preproductie.digipoort.logius.nl.cert"; // voorbeeld van preproductie service certificaat

        // bericht gegevens
        const string BERICHTSOORT = "Aangifte_LH";
        const string AANTELEVERENBESTAND = @"C:\temp\digipoort\000000000L01_2026_01_1.xml"; // bijvoorbeeld uw loonaangifte xml bestand
        const string UNIEKAANLEVERKENMERK = "Happyflow"; // uw uniek aanleveringskenmerk
        const string IDENTITEITBELANGHEBBENDE_NUMMER = "000000000L01";
        const string IDENTITEITBELANGHEBBENDE_TYPE = "LHnr";

        static void Main(string[] args)
        {
            X509Certificate2 clientCertificate = new X509Certificate2(CLIENT_CERTIFICATE, CLIENT_CERTIFICATE_PASSWORD);

            WusConnectionProfile digipoort = new WusConnectionProfile()
            {
                // "Aansluit Suite" test environment server certificate provided - valid until December 2019
                ServerCertificate = new X509Certificate2(SERVER_CERTIFICATE),
                EndpointAanleverService = "https://wus.preproductie.digipoort.logius.nl/wus/2.0/aanleverservice/1.2",
                EndpointStatusInformatieService = "https://wus.preproductie.digipoort.logius.nl/wus/2.0/statusinformatieservice/1.2",
                EndpointOphaalService = "https://wus.preproductie.digipoort.logius.nl/wus/2.0/ophaalservice/1.2",
                AuspService = "http://geenausp.nl",
                ConnectionStyle = WusConnectionProfile.WusConnectionStyle.asynchronous
            };

            WusClient wusClient = new WusClient(digipoort, clientCertificate);


            Console.WriteLine("Start aanleveren");
            AanleverTest(wusClient);

            Console.WriteLine();
            Console.WriteLine("Start ophalen");
            Thread.Sleep(4000);

            OphaalTest(wusClient);

            Console.WriteLine();
            Console.WriteLine("Press any key...");

            Console.ReadKey();
        }

        private static void OphaalTest(WusClient wusClient)
        {
            ServiceReferenceOphalen.BerichtLijstResultaat[] berichtenLijst = null;

            try
            {
                ServiceReferenceOphalen.getBerichtenLijstRequest1 requestLijst = wusClient.CreateBerichtenLijstRequest("TEST", DateTime.Now.Subtract(new TimeSpan(365, 0, 0, 0)), DateTime.Now);
                ServiceReferenceOphalen.getBerichtenLijstResponse1 responseLijst = wusClient.OphalenBerichtenLijst(requestLijst);

                berichtenLijst = responseLijst.getBerichtenLijstResponse.getBerichtenLijstReturn;
            }
            catch (FaultException<ServiceReferenceOphalen.foutType> ex)
            {
                Console.WriteLine(ex.Message);

                if (ex.Detail != null)
                    Console.WriteLine($"{ex.Detail.foutcode} - {ex.Detail.foutbeschrijving}");
            }
            // Handling known exceptions, including EndpointNotFoundException, MessageSecurityException, SecurityNegotiationException (derived from CommunicationException)
            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            if (berichtenLijst != null)
            {
                Console.WriteLine("Berichtenlijst:");
                foreach (ServiceReferenceOphalen.BerichtLijstResultaat bericht in berichtenLijst)
                    Console.WriteLine($"Bericht: {bericht.berichtsoort} - {bericht.identiteitBelanghebbende?.ToString()} - {bericht.kenmerk}");

                Thread.Sleep(5000);
                Console.WriteLine();

                foreach (ServiceReferenceOphalen.BerichtLijstResultaat bericht in berichtenLijst)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Ophalen bericht {bericht.kenmerk}");
                    OphalenBericht(wusClient, bericht.kenmerk);
                    Thread.Sleep(2000);
                }
            }
            else
            {
                Console.WriteLine("Er zijn geen berichten");
            }
        }

        private static void OphalenBericht(WusClient wusClient, string kenmerk)
        {
            try
            {
                ServiceReferenceOphalen.getBerichtenKenmerkRequest1 requestBericht = wusClient.CreateBerichtenKenmerkRequest(kenmerk);
                ServiceReferenceOphalen.getBerichtenKenmerkResponse1 responseBericht = wusClient.OphalenBericht(requestBericht);

                if (responseBericht != null)
                {
                    ServiceReferenceOphalen.BerichtResultaat[] result = responseBericht.getBerichtenKenmerkResponse.getBerichtenKenmerkReturn;
                    foreach (ServiceReferenceOphalen.BerichtResultaat r in result)
                        Console.WriteLine($"{r.berichtkenmerk} - {r.berichtInhoud.bestandsnaam} - {r.berichtInhoud.mimeType}");
                }
            }
            catch (FaultException<ServiceReferenceOphalen.foutType> ex)
            {
                Console.WriteLine(ex.Message);

                if (ex.Detail != null)
                    Console.WriteLine($"{ex.Detail.foutcode} - {ex.Detail.foutbeschrijving}");
            }
            // Handling known exceptions, including EndpointNotFoundException, MessageSecurityException, SecurityNegotiationException (derived from CommunicationException)
            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void AanleverTest(WusClient wusClient)
        {
            Console.WriteLine("Creating request");

            ServiceReferenceAanleveren.identiteitType identity = new ServiceReferenceAanleveren.identiteitType(IDENTITEITBELANGHEBBENDE_NUMMER, IDENTITEITBELANGHEBBENDE_TYPE);
            aanleverenRequest aanleverRequest = wusClient.CreateAanleverRequest(UNIEKAANLEVERKENMERK, BERICHTSOORT, identity, "Intermediair", AANTELEVERENBESTAND);

            Console.WriteLine("Sending request");
            Console.WriteLine();

            Stopwatch timer = new Stopwatch();
            timer.Start();

            aanleverenResponse aanleverResponse = null;
            try
            {
                aanleverResponse = wusClient.Aanleveren(aanleverRequest);
            }
            catch (FaultException<ServiceReferenceAanleveren.foutType> ex)
            {
                Console.WriteLine(ex.Message);

                if (ex.Detail != null)
                    Console.WriteLine($"{ex.Detail.foutcode} - {ex.Detail.foutbeschrijving}");
            }
            // Handling known exceptions, including EndpointNotFoundException, MessageSecurityException, SecurityNegotiationException (derived from CommunicationException)
            catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            if (aanleverResponse?.aanleverResponse == null)
            {
                Console.WriteLine();
                Console.WriteLine("No valid response received");
                //Console.ReadKey();
                Thread.Sleep(5000);
                return;
            }

            timer.Stop();

            Console.WriteLine($"Message ID (kenmerk): {aanleverResponse.aanleverResponse?.kenmerk}");
            Console.WriteLine($"Time stamp: {aanleverResponse.aanleverResponse?.tijdstempelAangeleverd}");
            Console.WriteLine($"Received response in {timer.Elapsed}");
            Console.WriteLine();

            if (wusClient.Profile.ConnectionStyle == WusConnectionProfile.WusConnectionStyle.asynchronous)
            {
                Console.WriteLine("Waiting 8 seconds before retrieving status...");
                Console.WriteLine();

                Thread.Sleep(8000);

                try
                {
                    getStatussenProcesResponse1 statusResponse = wusClient.StatusInformatie(aanleverResponse.aanleverResponse?.kenmerk);

                    if (statusResponse != null && statusResponse.getStatussenProcesResponse.getStatussenProcesReturn.Any())
                    {
                        StatusResultaat firstResult = statusResponse.getStatussenProcesResponse.getStatussenProcesReturn[0];

                        Console.WriteLine($"Kenmerk: {firstResult.kenmerk}");
                        Console.WriteLine($"Identity: {firstResult.identiteitBelanghebbende}");
                        Console.WriteLine();

                        foreach (StatusResultaat status in statusResponse.getStatussenProcesResponse.getStatussenProcesReturn)
                            Console.WriteLine($"Status: {status.statuscode} - {status.tijdstempelStatus} - {status.statusomschrijving}");
                    }
                    else
                        Console.WriteLine("Unable to query status");
                }
                catch (FaultException<ServiceReferenceStatusInformatie.foutType> ex)
                {
                    Console.WriteLine(ex.Message);

                    if (ex.Detail != null)
                        Console.WriteLine($"{ex.Detail.foutcode} - {ex.Detail.foutbeschrijving}");
                }
                // Handling known exceptions, including EndpointNotFoundException, MessageSecurityException, SecurityNegotiationException (derived from CommunicationException)
                catch (Exception ex) when (ex is CommunicationException || ex is TimeoutException)
                {
                    Console.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
    }
}
