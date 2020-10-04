using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
    public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
    {
        private readonly IDataContext DataContext;
        private IPathProvider _templatePathProvider;
        public IViewGenerator View_Generator;
        internal readonly IConfiguration _configuration;
        private readonly ILogger<PdfApplicationDocumentGenerator> _logger;
        private readonly IPdfGenerator _pdfGenerator;

        public PdfApplicationDocumentGenerator(
            IDataContext dataContext,
            IPathProvider templatePathProvider,
            IViewGenerator viewGenerator,
            IConfiguration configuration,
            IPdfGenerator pdfGenerator,
            ILogger<PdfApplicationDocumentGenerator> logger)
        {
            if (dataContext != null)
                throw new ArgumentNullException(nameof(dataContext));

            DataContext = dataContext;
            _templatePathProvider = templatePathProvider ?? throw new ArgumentNullException("templatePathProvider");
            View_Generator = viewGenerator;
            _configuration = configuration ?? throw new ArgumentNullException("configuration");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pdfGenerator = pdfGenerator;
        }

        public byte[] Generate(Guid applicationId, string baseUri)
        {
            //Use SingleOrDefault to cater for null return
            Application application = DataContext.Applications.SingleOrDefault(app => app.Id == applicationId);

            if (application != null)
            {
                //Substring will result in incorrect url with 1 character
                //Ensure last slash is removed only
                if (baseUri.EndsWith("/"))
                    baseUri = baseUri.Substring(0, baseUri.Length - 1);

                string view;
                string path;

                //Do NULL check on PERSON object and throw exception
                if (application.Person == null)
                    throw new ArgumentNullException("Person");

                switch (application.State)
                {
                    case ApplicationState.Pending:
                        path = _templatePathProvider.Get("PendingApplication");
                        PendingApplicationViewModel PendingApplicationViewModel = new PendingApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            //changed for consistency
                            FullName = string.Format("{0} {1}", application.Person.FirstName, application.Person.Surname),
                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature
                        };
                        view = View_Generator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), PendingApplicationViewModel);
                        break;
                    case ApplicationState.Activated:
                        path = _templatePathProvider.Get("ActivatedApplication");
                        ActivatedApplicationViewModel ActivatedApplicationViewModel = new ActivatedApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            //changed for consistency
                            FullName = string.Format("{0} {1}", application.Person.FirstName, application.Person.Surname),
                            LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
                            //Assume that Legal entity can have a value evn when IsLegalEntity = 0
                            PortfolioFunds = application.Products.SelectMany(p => p.Funds),
                            PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
                                                            //Below calculation results in calculating tax ONLY, not amount + tax(Assuming TaxRate is a percentage i.e 0.15)
                                                            //.Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                                                            .Select(f => (f.Amount - f.Fees) + ((f.Amount - f.Fees) * _configuration.TaxRate))
                                                            .Sum(),
                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature
                        };
                        view = View_Generator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), ActivatedApplicationViewModel);
                        break;
                    case ApplicationState.InReview:
                        path = _templatePathProvider.Get("InReviewApplication");
                        //strongly typed for readability purposes
                        string inReviewMessage = "Your application has been placed in review" +
                                            application.CurrentReview.Reason switch
                                            {
                                                { } reason when reason.Contains("address") =>
                                                    " pending outstanding address verification for FICA purposes.",
                                                { } reason when reason.Contains("bank") =>
                                                    " pending outstanding bank account verification.",
                                                _ =>
                                                    " because of suspicious account behaviour. Please contact support ASAP."
                                            };
                        //make below model into a block like the others
                        InReviewApplicationViewModel inReviewApplicationViewModel = new InReviewApplicationViewModel
                        {
                            ReferenceNumber = application.ReferenceNumber,
                            State = application.State.ToDescription(),
                            FullName = string.Format("{0} {1}", application.Person.FirstName, application.Person.Surname),
                            LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
                            PortfolioFunds = application.Products.SelectMany(p => p.Funds),
                            PortfolioTotalAmount =
                                                   application.Products.SelectMany(p => p.Funds)
                                                    //Below calculation results in calculating tax ONLY, not amount + tax(Assuming TaxRate is a percentage i.e 0.15)
                                                    //.Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
                                                    .Select(f => (f.Amount - f.Fees) + ((f.Amount - f.Fees) * _configuration.TaxRate))
                                                   .Sum(),
                            InReviewMessage = inReviewMessage,
                            InReviewInformation = application.CurrentReview,
                            AppliedOn = application.Date,
                            SupportEmail = _configuration.SupportEmail,
                            Signature = _configuration.Signature,

                        };

                        //Changed for consistency
                        view = View_Generator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), inReviewApplicationViewModel);
                        break;
                    default:
                        _logger.LogWarning(
                         $"The application is in state '{application.State}' and no valid document can be generated for it.");
                        //throw error instead of return null
                        throw new UnrecognisedApplicationStateException($"The application is in state '{application.State}' and no valid document can be generated for it.");

                }

                var pdfOptions = new PdfOptions
                {
                    PageNumbers = PageNumbers.Numeric,
                    HeaderOptions = new HeaderOptions
                    {
                        HeaderRepeat = HeaderRepeat.FirstPageOnly,
                        HeaderHtml = PdfConstants.Header
                    }
                };
                var pdf = _pdfGenerator.GenerateFromHtml(view, pdfOptions);
                return pdf.ToBytes();
            }
            else
            {

                _logger.LogWarning(
                    $"No application found for id '{applicationId}'");
                throw new NoApplicationFoundException($"No application found for id '{applicationId}'");
                //throw error instead of return null
            }
        }
    }
}
