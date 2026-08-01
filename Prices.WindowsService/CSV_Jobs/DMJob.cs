using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;
using Prices.WindowsService.POCO;
using System.Globalization;
using System.Net.Http;
using System.Text;

namespace Prices.WindowsService.CSV_Jobs
{
    public class DMJob : Base<DMJob>
    {
        private readonly ILogger<DMJob> _logger;
        private readonly string _basePageUrl = "https://www.dm.hr/novo/promocije/nove-oznake-cijena-i-vazeci-cjenik-u-dm-u-2906632";

        public DMJob(ILogger<DMJob> Logger, BaseJobDependencies Dependencies) 
            : base(Logger, Dependencies)
        {
            _logger = Logger;

        }

        public override async Task Work()
        {

            try
            {
                RetailerDataPOCO retailerData = await GetRetailerBasicDataAsync(RetailersEnum.DM);

                if (retailerData != null)
                {
                    string csvText = string.Empty;

                    using (MemoryStream documentStream = await GetExcelDocumentStreamAsync())
                    {
                        csvText = await GetCsvTextAsync(documentStream);
                    }

                    if (!string.IsNullOrEmpty(csvText))
                    {
                        List<ImportLog> importLogs = new List<ImportLog>();

                        using (var csvFile = File.CreateText(retailerData.csvDirectory + "\\" + retailerData.retailerId + ".csv"))
                        {
                            await csvFile.WriteAsync(csvText);

                            importLogs.Add(new ImportLog { retailerID = retailerData.retailerId });
                        }

                        await InsertImportLogs(importLogs);
                        SetSleepMinutes(1440);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        private async Task<MemoryStream> GetExcelDocumentStreamAsync()
        {
            using (var playwright = await Playwright.CreateAsync())
            {
                await using (var browser = await playwright.Chromium.LaunchAsync(new()
                {
                    Headless = true,
                    Channel = "msedge"
                }))
                {
                    var context = await browser.NewContextAsync();
                    var page = await context.NewPageAsync();
                    string href = String.Empty;

                    await page.GotoAsync(_basePageUrl);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    await page.WaitForSelectorAsync("a", new PageWaitForSelectorOptions { Timeout = 3000 });
                    var anchors = page.Locator("a");
                    int anchorCount = await anchors.CountAsync();

                    string todaysDate = DateTime.Now.ToString("d.M.yyyy");

                    for (int i = 0; i < anchorCount; i++)
                    {
                        var anchor = anchors.Nth(i);
                        var text = await anchor.InnerTextAsync();
                        if (text.StartsWith("Važeći cjenik dm_", StringComparison.OrdinalIgnoreCase)
                            && text.Contains(todaysDate))
                        {
                            href = await anchor.First.GetAttributeAsync("href");
                            break;
                        }
                    }

                    if (String.IsNullOrEmpty(href))
                    {
                        _logger.LogInformation($"{_jobName} excel download link was not found.");
                        return null;
                    }

                    var requestContext = await playwright.APIRequest.NewContextAsync(new()
                    {
                        StorageState = await context.StorageStateAsync()
                    });

                    var response = await requestContext.GetAsync(href);

                    if (!response.Ok)
                    {
                        _logger.LogInformation($"{_jobName} excel download response was not OK.");
                        return null;
                    }

                    var bytes = await response.BodyAsync();
                    var memoryStream = new MemoryStream();
                    await memoryStream.WriteAsync(bytes, 0, bytes.Length);
                    memoryStream.Position = 0;
                    return memoryStream;
                }
            }
        }

        private async Task<string> GetCsvTextAsync(MemoryStream documentStream)
        {
            string result = string.Empty;
            var culture = CultureInfo.CreateSpecificCulture("hr-HR");

            using (SpreadsheetDocument document = SpreadsheetDocument.Open(documentStream, true))
            {
                WorkbookPart workbookPart = document.WorkbookPart;

                Workbook workbook = workbookPart.Workbook;
                Sheet sheet = workbook.Descendants<Sheet>().Where(x => x.Name == "Važeći cjenik").FirstOrDefault();

                WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                Worksheet worksheet = worksheetPart.Worksheet;

                // fix for cells using shared string table data
                SharedStringTablePart sharedStringPart = workbookPart.SharedStringTablePart;
                foreach (var cell in worksheet.Descendants<Cell>())
                {
                    if (cell.DataType == "s")
                    {
                        int index = Int32.Parse(cell.CellValue.Text);
                        var value = sharedStringPart.RootElement.Elements<SharedStringItem>().ElementAt(index);
                        cell.RemoveAllChildren();
                        cell.DataType = CellValues.InlineString;
                        cell.Append(new InlineString(new Text(value.Text.Text.Replace("\"", ""))));
                    }
                }

                SheetData sheetData = worksheet.GetFirstChild<SheetData>();

                IEnumerable<Row> rows = sheetData.Descendants<Row>();

                var validationCell = rows.ElementAt<Row>(1).Descendants<Cell>().ElementAt(0).InnerText.ToString().ToLower().Replace(" ", "");

                if (validationCell == "naziv+šifra")
                {
                    StringBuilder sbCsvText = new StringBuilder();
                    sbCsvText.AppendLine($"\"naziv\";\"šifra\";\"marka\";\"barkod\";\"kategorija proizvoda\";" +
                        $"\"neto količina\";\"Jedinica mjere\";\"Cijena za jedinicu mjere\";\"dostupno samo online\";" +
                        $"\"MPC\";\"MPC za vrijeme posebnog oblika prodaje (Rasprodaja proizvoda koji izlaze iz asortimana)\";" +
                        $"\"Najniža cijena u posljednjih 30 dana prije rasprodaje\";\"sidrena cijena na 2.5.2025. ili na datum ulistanja\"");
                    IEnumerable<Row> filteredRows = rows.Skip(2);


                    foreach (Row row in filteredRows)
                    {
                        StringBuilder sbRowText = new StringBuilder();

                        foreach (Cell cell in row)
                        {
                            //todo 
                            // cell data type and format

                            if (cell.CellValue != null)
                            {
                                decimal csvDecimal;
                                Decimal.TryParse(cell.InnerText.ToString().Trim(), System.Globalization.CultureInfo.InvariantCulture, out csvDecimal);
                                sbRowText.Append(String.Format("{0};", csvDecimal));
                            }
                            else
                            {
                                switch (cell.DataType?.ToString())
                                {
                                    case "inlineStr":
                                        sbRowText.Append(String.Format("\"{0}\";", cell.InnerText.Trim()));
                                        break;

                                    case "n":
                                        decimal csvDecimal;
                                        Decimal.TryParse(cell.InnerText.ToString().Trim(), System.Globalization.CultureInfo.InvariantCulture, out csvDecimal);
                                        sbRowText.Append(String.Format("{0};", csvDecimal));
                                        break;

                                    default:
                                        sbRowText.Append(String.Format("{0};", cell.InnerText.Trim()));
                                        break;
                                }
                            }  
                        }

                        sbCsvText.AppendLine(sbRowText.ToString().TrimEnd(';'));
                    }

                    result = sbCsvText.ToString();
                }
            }

            return result;
        }
    }
}
