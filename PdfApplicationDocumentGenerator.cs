using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
    public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
    {
        //Access modifiers and naming convention corrected. These fields only used here
        private readonly IDataContext _dataContext;
        private static IPathProvider _templatePathProvider;
        private static IViewGenerator _viewGenerator;
        private static IConfiguration _configuration;
        private static ILogger<PdfApplicationDocumentGenerator> _logger;
        private static IPdfGenerator _pdfGenerator;

        public PdfApplicationDocumentGenerator(
            IDataContext dataContext,
            IPathProvider templatePathProvider,
            IViewGenerator viewGenerator,
            IConfiguration configuration,
            IPdfGenerator pdfGenerator,
            ILogger<PdfApplicationDocumentGenerator> logger)
        {
        //removed questionable null check
        
            //Change for consistency
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
            _templatePathProvider = templatePathProvider ?? throw new ArgumentNullException(nameof(templatePathProvider));
            _viewGenerator = viewGenerator ?? throw new ArgumentNullException(nameof(viewGenerator));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
        }

        public byte[] Generate(Guid applicationId, string baseUri)
        {
            //Use SingleOrDefault to cater for null return
            Application application = _dataContext.Applications.SingleOrDefault(app => app.Id == applicationId);

            if (application != null)
            {
                //Substring will result in incorrect baseUri with 1 character
                //Ensure last slash is removed only
                if (baseUri.EndsWith("/"))
                    baseUri = baseUri.Substring(0, baseUri.Length - 1);

                var view = "";

                //Do NULL check on PERSON object and throw exception
                if (application.Person == null)
                    throw new ArgumentNullException("Person");
                
                //view retrieved by method
                view = GetViewByState(application,baseUri)
                //seperate for readability
                if(view != null)
                {
                    return GeneratePDF(view);
                }
                else {
                    return null;
                }
            }
            else
            {

                _logger.LogWarning(
                    $"No application found for id '{applicationId}'");
                return null;
                //throw new NoApplicationFoundException($"No application found for id '{applicationId}'");
                //throw error instead of return null
            }
        }
        
        private string GetViewByState(Application application, string baseUri)
        {
            var view="";
            switch (application.State)
               {
                    case ApplicationState.Pending:
                        view = GetPendingApplicationView(application, baseUri);
                        break;
                    case ApplicationState.Activated:
                        view = GetActivatedApplicationView(application, baseUri);
                        break;
                    case ApplicationState.InReview:
                        view = GetInReviewApplicationView(application, baseUri);
                        break;
                    default:
                        _logger.LogWarning(
                         $"The application is in state '{application.State}' and no valid document can be generated for it.");
                         return null;
                        //throw error instead of return null
                        //throw new UnrecognisedApplicationStateException($"The application is in state '{application.State}' and no valid document can be generated for it.");

                }
              return view;
        }
                
        private byte[] GeneratePDF(string view)
        {
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
        
        
        private static string GetPendingApplicationView(Application application, string baseUri)
        {
            
            var path = _templatePathProvider.Get("PendingApplication");

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
            return _viewGenerator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), PendingApplicationViewModel);

        }
        private static string GetActivatedApplicationView(Application application, string baseUri)
        {

            var path = _templatePathProvider.Get("ActivatedApplication");
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
            return _viewGenerator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), ActivatedApplicationViewModel);

        }
        private static string GetInReviewApplicationView(Application application, string baseUri)
        {
            var path = _templatePathProvider.Get("InReviewApplication");
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
            return _viewGenerator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), inReviewApplicationViewModel);

        }
    }


}
