using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;

internal static class ReferenceDataSeedData
{
    public static ReferenceDataSeedModel Create()
    {
        var states = CreateStates();
        var zipCodes = CreateZipCodes();
        var plans = CreatePlans();

        return new ReferenceDataSeedModel(
            states,
            zipCodes,
            plans);
    }

    private static List<State> CreateStates()
    {
        return
        [
            new() { StateCode = "AL", Name = "Alabama", IsActive = true },
            new() { StateCode = "AK", Name = "Alaska", IsActive = true },
            new() { StateCode = "AZ", Name = "Arizona", IsActive = true },
            new() { StateCode = "AR", Name = "Arkansas", IsActive = true },
            new() { StateCode = "CA", Name = "California", IsActive = true },
            new() { StateCode = "CO", Name = "Colorado", IsActive = true },
            new() { StateCode = "CT", Name = "Connecticut", IsActive = true },
            new() { StateCode = "DE", Name = "Delaware", IsActive = true },
            new() { StateCode = "FL", Name = "Florida", IsActive = true },
            new() { StateCode = "GA", Name = "Georgia", IsActive = true },
            new() { StateCode = "HI", Name = "Hawaii", IsActive = true },
            new() { StateCode = "ID", Name = "Idaho", IsActive = true },
            new() { StateCode = "IL", Name = "Illinois", IsActive = true },
            new() { StateCode = "IN", Name = "Indiana", IsActive = true },
            new() { StateCode = "IA", Name = "Iowa", IsActive = true },
            new() { StateCode = "KS", Name = "Kansas", IsActive = true },
            new() { StateCode = "KY", Name = "Kentucky", IsActive = true },
            new() { StateCode = "LA", Name = "Louisiana", IsActive = true },
            new() { StateCode = "ME", Name = "Maine", IsActive = true },
            new() { StateCode = "MD", Name = "Maryland", IsActive = true },
            new() { StateCode = "MA", Name = "Massachusetts", IsActive = true },
            new() { StateCode = "MI", Name = "Michigan", IsActive = true },
            new() { StateCode = "MN", Name = "Minnesota", IsActive = true },
            new() { StateCode = "MS", Name = "Mississippi", IsActive = true },
            new() { StateCode = "MO", Name = "Missouri", IsActive = true },
            new() { StateCode = "MT", Name = "Montana", IsActive = true },
            new() { StateCode = "NE", Name = "Nebraska", IsActive = true },
            new() { StateCode = "NV", Name = "Nevada", IsActive = true },
            new() { StateCode = "NH", Name = "New Hampshire", IsActive = true },
            new() { StateCode = "NJ", Name = "New Jersey", IsActive = true },
            new() { StateCode = "NM", Name = "New Mexico", IsActive = true },
            new() { StateCode = "NY", Name = "New York", IsActive = true },
            new() { StateCode = "NC", Name = "North Carolina", IsActive = true },
            new() { StateCode = "ND", Name = "North Dakota", IsActive = true },
            new() { StateCode = "OH", Name = "Ohio", IsActive = true },
            new() { StateCode = "OK", Name = "Oklahoma", IsActive = true },
            new() { StateCode = "OR", Name = "Oregon", IsActive = true },
            new() { StateCode = "PA", Name = "Pennsylvania", IsActive = true },
            new() { StateCode = "RI", Name = "Rhode Island", IsActive = true },
            new() { StateCode = "SC", Name = "South Carolina", IsActive = true },
            new() { StateCode = "SD", Name = "South Dakota", IsActive = true },
            new() { StateCode = "TN", Name = "Tennessee", IsActive = true },
            new() { StateCode = "TX", Name = "Texas", IsActive = true },
            new() { StateCode = "UT", Name = "Utah", IsActive = true },
            new() { StateCode = "VT", Name = "Vermont", IsActive = true },
            new() { StateCode = "VA", Name = "Virginia", IsActive = true },
            new() { StateCode = "WA", Name = "Washington", IsActive = true },
            new() { StateCode = "WV", Name = "West Virginia", IsActive = true },
            new() { StateCode = "WI", Name = "Wisconsin", IsActive = true },
            new() { StateCode = "WY", Name = "Wyoming", IsActive = true },
            new() { StateCode = "DC", Name = "District of Columbia", IsActive = true }
        ];
    }

    private static List<ZipCode> CreateZipCodes()
    {
        var definitions = new (string PostalCode, string StateCode, string City)[]
        {
            ("35004", "AL", "Moody"),
            ("35005", "AL", "Adamsville"),
            ("35006", "AL", "Adger"),
            ("99501", "AK", "Anchorage"),
            ("99502", "AK", "Anchorage"),
            ("99503", "AK", "Anchorage"),
            ("85001", "AZ", "Phoenix"),
            ("85003", "AZ", "Phoenix"),
            ("85004", "AZ", "Phoenix"),
            ("72201", "AR", "Little Rock"),
            ("72202", "AR", "Little Rock"),
            ("72204", "AR", "Little Rock"),
            ("90001", "CA", "Los Angeles"),
            ("90002", "CA", "Los Angeles"),
            ("94102", "CA", "San Francisco"),
            ("80202", "CO", "Denver"),
            ("80203", "CO", "Denver"),
            ("80903", "CO", "Colorado Springs"),
            ("06103", "CT", "Hartford"),
            ("06105", "CT", "Hartford"),
            ("06810", "CT", "Danbury"),
            ("19702", "DE", "Newark"),
            ("19801", "DE", "Wilmington"),
            ("19901", "DE", "Dover"),
            ("32003", "FL", "Fleming Island"),
            ("33101", "FL", "Miami"),
            ("33602", "FL", "Tampa"),
            ("30303", "GA", "Atlanta"),
            ("30305", "GA", "Atlanta"),
            ("31401", "GA", "Savannah"),
            ("96813", "HI", "Honolulu"),
            ("96814", "HI", "Honolulu"),
            ("96720", "HI", "Hilo"),
            ("83201", "ID", "Pocatello"),
            ("83702", "ID", "Boise"),
            ("83814", "ID", "Coeur d'Alene"),
            ("60601", "IL", "Chicago"),
            ("60602", "IL", "Chicago"),
            ("62701", "IL", "Springfield"),
            ("46204", "IN", "Indianapolis"),
            ("46802", "IN", "Fort Wayne"),
            ("47404", "IN", "Bloomington"),
            ("50309", "IA", "Des Moines"),
            ("52240", "IA", "Iowa City"),
            ("52401", "IA", "Cedar Rapids"),
            ("66101", "KS", "Kansas City"),
            ("67202", "KS", "Wichita"),
            ("66603", "KS", "Topeka"),
            ("40202", "KY", "Louisville"),
            ("40507", "KY", "Lexington"),
            ("42101", "KY", "Bowling Green"),
            ("70112", "LA", "New Orleans"),
            ("70802", "LA", "Baton Rouge"),
            ("71101", "LA", "Shreveport"),
            ("04101", "ME", "Portland"),
            ("04330", "ME", "Augusta"),
            ("04901", "ME", "Waterville"),
            ("21201", "MD", "Baltimore"),
            ("21401", "MD", "Annapolis"),
            ("20850", "MD", "Rockville"),
            ("02108", "MA", "Boston"),
            ("01608", "MA", "Worcester"),
            ("01103", "MA", "Springfield"),
            ("48201", "MI", "Detroit"),
            ("48933", "MI", "Lansing"),
            ("48104", "MI", "Ann Arbor"),
            ("55401", "MN", "Minneapolis"),
            ("55102", "MN", "Saint Paul"),
            ("55802", "MN", "Duluth"),
            ("39201", "MS", "Jackson"),
            ("39530", "MS", "Biloxi"),
            ("38804", "MS", "Tupelo"),
            ("63101", "MO", "St. Louis"),
            ("64106", "MO", "Kansas City"),
            ("65201", "MO", "Columbia"),
            ("59101", "MT", "Billings"),
            ("59601", "MT", "Helena"),
            ("59715", "MT", "Bozeman"),
            ("68102", "NE", "Omaha"),
            ("68508", "NE", "Lincoln"),
            ("68801", "NE", "Grand Island"),
            ("89101", "NV", "Las Vegas"),
            ("89501", "NV", "Reno"),
            ("89701", "NV", "Carson City"),
            ("03101", "NH", "Manchester"),
            ("03301", "NH", "Concord"),
            ("03801", "NH", "Portsmouth"),
            ("07102", "NJ", "Newark"),
            ("07302", "NJ", "Jersey City"),
            ("08608", "NJ", "Trenton"),
            ("87102", "NM", "Albuquerque"),
            ("87501", "NM", "Santa Fe"),
            ("88001", "NM", "Las Cruces"),
            ("10001", "NY", "New York"),
            ("11201", "NY", "Brooklyn"),
            ("14202", "NY", "Buffalo"),
            ("27601", "NC", "Raleigh"),
            ("28202", "NC", "Charlotte"),
            ("28801", "NC", "Asheville"),
            ("58102", "ND", "Fargo"),
            ("58501", "ND", "Bismarck"),
            ("58701", "ND", "Minot"),
            ("43215", "OH", "Columbus"),
            ("44113", "OH", "Cleveland"),
            ("45202", "OH", "Cincinnati"),
            ("73102", "OK", "Oklahoma City"),
            ("74103", "OK", "Tulsa"),
            ("73401", "OK", "Ardmore"),
            ("97204", "OR", "Portland"),
            ("97401", "OR", "Eugene"),
            ("97701", "OR", "Bend"),
            ("19103", "PA", "Philadelphia"),
            ("15222", "PA", "Pittsburgh"),
            ("17101", "PA", "Harrisburg"),
            ("02903", "RI", "Providence"),
            ("02840", "RI", "Newport"),
            ("02886", "RI", "Warwick"),
            ("29201", "SC", "Columbia"),
            ("29401", "SC", "Charleston"),
            ("29601", "SC", "Greenville"),
            ("57104", "SD", "Sioux Falls"),
            ("57401", "SD", "Aberdeen"),
            ("57701", "SD", "Rapid City"),
            ("37203", "TN", "Nashville"),
            ("37402", "TN", "Chattanooga"),
            ("38103", "TN", "Memphis"),
            ("73301", "TX", "Austin"),
            ("75001", "TX", "Addison"),
            ("77002", "TX", "Houston"),
            ("84101", "UT", "Salt Lake City"),
            ("84401", "UT", "Ogden"),
            ("84770", "UT", "St. George"),
            ("05401", "VT", "Burlington"),
            ("05602", "VT", "Montpelier"),
            ("05701", "VT", "Rutland"),
            ("23219", "VA", "Richmond"),
            ("23510", "VA", "Norfolk"),
            ("22902", "VA", "Charlottesville"),
            ("98101", "WA", "Seattle"),
            ("99201", "WA", "Spokane"),
            ("98501", "WA", "Olympia"),
            ("25301", "WV", "Charleston"),
            ("26505", "WV", "Morgantown"),
            ("25701", "WV", "Huntington"),
            ("53202", "WI", "Milwaukee"),
            ("53703", "WI", "Madison"),
            ("54301", "WI", "Green Bay"),
            ("82001", "WY", "Cheyenne"),
            ("82601", "WY", "Casper"),
            ("83001", "WY", "Jackson"),
            ("20001", "DC", "Washington"),
            ("20002", "DC", "Washington"),
            ("20003", "DC", "Washington")
        };

        return definitions
            .Select(x => new ZipCode
            {
                PostalCode = x.PostalCode,
                StateCode = x.StateCode,
                City = x.City,
                IsActive = true
            })
            .ToList();
    }

    private static List<Plan> CreatePlans()
    {
        return
        [
            new()
            {
                PlanId = "COM-TX-1000",
                PlanName = "Kaleido Commercial Choice TX",
                LineOfBusiness = LineOfBusiness.Commercial,
                StateCode = "TX",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = null,
                IsActive = true
            },
            new()
            {
                PlanId = "COM-FL-2000",
                PlanName = "Kaleido Commercial Select FL",
                LineOfBusiness = LineOfBusiness.Commercial,
                StateCode = "FL",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = null,
                IsActive = true
            },
            new()
            {
                PlanId = "COM-CA-3000",
                PlanName = "Kaleido Commercial Plus CA",
                LineOfBusiness = LineOfBusiness.Commercial,
                StateCode = "CA",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = new DateOnly(2026, 12, 31),
                IsActive = true
            },
            new()
            {
                PlanId = "MEDI-TX-1000",
                PlanName = "Kaleido Medicare Gold TX",
                LineOfBusiness = LineOfBusiness.Medicare,
                StateCode = "TX",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = null,
                IsActive = true
            },
            new()
            {
                PlanId = "MEDI-FL-2000",
                PlanName = "Kaleido Medicare Advantage FL",
                LineOfBusiness = LineOfBusiness.Medicare,
                StateCode = "FL",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = null,
                IsActive = true
            },
            new()
            {
                PlanId = "MEDI-OH-3000",
                PlanName = "Kaleido Medicare Value OH",
                LineOfBusiness = LineOfBusiness.Medicare,
                StateCode = "OH",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = new DateOnly(2026, 12, 31),
                IsActive = true
            },
            new()
            {
                PlanId = "MCD-TX-1000",
                PlanName = "Kaleido Medicaid Star TX",
                LineOfBusiness = LineOfBusiness.Medicaid,
                StateCode = "TX",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = null,
                IsActive = true
            },
            new()
            {
                PlanId = "MCD-GA-2000",
                PlanName = "Kaleido Medicaid Access GA",
                LineOfBusiness = LineOfBusiness.Medicaid,
                StateCode = "GA",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = null,
                IsActive = true
            },
            new()
            {
                PlanId = "MCD-NY-3000",
                PlanName = "Kaleido Medicaid Community NY",
                LineOfBusiness = LineOfBusiness.Medicaid,
                StateCode = "NY",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = new DateOnly(2026, 12, 31),
                IsActive = true
            },
            new()
            {
                PlanId = "EX-CA-1000",
                PlanName = "Kaleido Exchange Bronze CA",
                LineOfBusiness = LineOfBusiness.Exchange,
                StateCode = "CA",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = null,
                IsActive = true
            },
            new()
            {
                PlanId = "EX-FL-2000",
                PlanName = "Kaleido Exchange Silver FL",
                LineOfBusiness = LineOfBusiness.Exchange,
                StateCode = "FL",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = null,
                IsActive = true
            },
            new()
            {
                PlanId = "EX-NC-3000",
                PlanName = "Kaleido Exchange Gold NC",
                LineOfBusiness = LineOfBusiness.Exchange,
                StateCode = "NC",
                EffectiveDate = new DateOnly(2025, 1, 1),
                TerminationDate = new DateOnly(2026, 12, 31),
                IsActive = true
            }
        ];
    }
}

internal sealed record ReferenceDataSeedModel(
    IReadOnlyList<State> States,
    IReadOnlyList<ZipCode> ZipCodes,
    IReadOnlyList<Plan> Plans);
