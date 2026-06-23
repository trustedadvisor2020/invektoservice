using Bogus;

namespace Chatinbox.Tests._Shared.DataGenerators;

/// <summary>
/// Bogus-based Turkish fake customer data generator.
/// Generates realistic Turkish names, phone numbers, and emails.
/// </summary>
public static class TurkishCustomerGenerator
{
    private static readonly Faker Faker = new("tr");

    public static (string Phone, string Name, string Email) GenerateCustomer()
    {
        var firstName = Faker.Name.FirstName();
        var lastName = Faker.Name.LastName();
        var name = $"{firstName} {lastName}";
        var phone = $"+905{Faker.Random.Int(300, 599):000}{Faker.Random.Int(1000000, 9999999):0000000}";
        var email = Faker.Internet.Email(firstName, lastName, "example.com");
        return (phone, name, email);
    }

    public static List<string> GeneratePhoneList(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => $"+905{Faker.Random.Int(300, 599):000}{Faker.Random.Int(1000000, 9999999):0000000}")
            .ToList();
    }

    public static string GenerateCompanyName() => Faker.Company.CompanyName();

    public static Dictionary<string, string> GenerateTemplateVariables()
    {
        var (_, name, _) = GenerateCustomer();
        return new Dictionary<string, string>
        {
            ["customer_name"] = name,
            ["company_name"] = GenerateCompanyName(),
            ["date"] = DateTime.Now.ToString("dd.MM.yyyy"),
            ["time"] = DateTime.Now.ToString("HH:mm")
        };
    }

    // Common Turkish customer messages for test scenarios
    public static readonly string[] CustomerMessages =
    [
        "Merhaba, urun hakkinda bilgi almak istiyorum",
        "Fiyat listesi gonderebilir misiniz?",
        "Siparis durumumu ogrenebilir miyim?",
        "Iade yapmak istiyorum",
        "Randevu almak istiyorum",
        "Kampanyalariniz hakkinda bilgi verir misiniz?",
        "Kargo ne zaman gelir?",
        "Beden tablosu var mi?",
        "Stokta var mi?",
        "Musteri hizmetleri ile gorusmek istiyorum"
    ];

    public static string RandomMessage() => Faker.PickRandom(CustomerMessages);
}
