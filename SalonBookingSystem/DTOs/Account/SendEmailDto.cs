namespace SalonBookingSystem.DTOs.Account
{
    public class SendEmailDto
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }

        public SendEmailDto(string to, string subject, string body)
        {
            To = to;
            Subject = subject;
            Body = body;
        }
    }
}
