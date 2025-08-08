namespace PA_Website.Models;

public class NewsletterSubscription
{
    public int Id { get; set; }
    public string Email { get; set; }
    public bool ConsentGiven { get; set; } = true;
}