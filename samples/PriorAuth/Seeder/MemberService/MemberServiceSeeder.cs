using Kaleido.Samples.PriorAuth.MemberService.Artifacts;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;
using ReferenceLineOfBusiness = Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.LineOfBusiness;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.MemberService;

internal sealed class MemberServiceSeeder(
    ServiceProjectContextFactory projectContextFactory,
    JsonAssetLoader jsonAssetLoader)
    : IDomainSeeder
{
    public SupportedDomain Domain => SupportedDomain.MemberService;

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var provider =
            projectContextFactory.CreateSqliteDbContextProvider<MemberDbContext>(
                connectionString: "Data Source=MemberService/data/memberservice.db");

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<MemberDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var assets = LoadAssets();
        var zipCodesByState = LoadZipCodesByState(assets.Settings.AllowedStates);
        var plansByState = LoadPlansByState(assets.Settings.AllowedStates);

        var members = new List<Member>(assets.Settings.MemberCount);
        var addresses = new List<MemberAddress>(assets.Settings.MemberCount + (assets.Settings.MemberCount / assets.Settings.SecondaryAddressModulo));
        var enrollments = new List<MemberEnrollment>(assets.Settings.MemberCount + (assets.Settings.MemberCount / assets.Settings.AdditionalEnrollmentModulo));

        for (var memberIndex = 1; memberIndex <= assets.Settings.MemberCount; memberIndex++)
        {
            var member = CreateMember(memberIndex, assets);
            members.Add(member);

            var primaryState = assets.Settings.AllowedStates[(memberIndex - 1) % assets.Settings.AllowedStates.Count];
            var primaryAddress = CreateAddress(memberIndex, 1, member.MemberId, primaryState, assets, zipCodesByState);
            addresses.Add(primaryAddress);

            enrollments.Add(
                CreateEnrollment(
                    memberIndex,
                    1,
                    member.MemberId,
                    primaryAddress.MemberAddressId,
                    primaryState,
                    assets.Settings,
                    plansByState));

            if (memberIndex % assets.Settings.AdditionalEnrollmentModulo != 0)
            {
                continue;
            }

            var secondaryState = assets.Settings.AllowedStates[memberIndex % assets.Settings.AllowedStates.Count];
            MemberAddress secondaryAddress;

            if (memberIndex % assets.Settings.SecondaryAddressModulo == 0
                && secondaryState != primaryState)
            {
                secondaryAddress = CreateAddress(memberIndex, 2, member.MemberId, secondaryState, assets, zipCodesByState);
                addresses.Add(secondaryAddress);
            }
            else
            {
                secondaryAddress = primaryAddress;
                secondaryState = primaryState;
            }

            enrollments.Add(
                CreateEnrollment(
                    memberIndex,
                    2,
                    member.MemberId,
                    secondaryAddress.MemberAddressId,
                    secondaryState,
                    assets.Settings,
                    plansByState));
        }

        dbContext.Members.AddRange(members);
        dbContext.MemberAddresses.AddRange(addresses);
        dbContext.MemberEnrollments.AddRange(enrollments);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private MemberSeedAssets LoadAssets()
    {
        var memberServicePath = "memberservice";

        var assets = new MemberSeedAssets
        {
            FirstNames = LoadRequiredStringList(memberServicePath, "first-names.json"),
            LastNames = LoadRequiredStringList(memberServicePath, "last-names.json"),
            StreetNames = LoadRequiredStringList(memberServicePath, "street-names.json"),
            StreetSuffixes = LoadRequiredStringList(memberServicePath, "street-suffixes.json"),
            AddressLine2Patterns = LoadRequiredStringList(memberServicePath, "address-line2-patterns.json"),
            Settings = jsonAssetLoader.Load<MemberSeedSettings>(Path.Combine(memberServicePath, "seed-settings.json"))
        };

        ValidateSettings(assets.Settings);
        return assets;
    }

    private List<string> LoadRequiredStringList(
        string basePath,
        string fileName)
    {
        var values = jsonAssetLoader.Load<List<string>>(Path.Combine(basePath, fileName));

        if (values.Count == 0)
        {
            throw new InvalidOperationException($"Asset '{fileName}' must contain at least one value.");
        }

        return values;
    }

    private static void ValidateSettings(MemberSeedSettings settings)
    {
        if (settings.MemberCount <= 0)
        {
            throw new InvalidOperationException("Member seed settings must define a positive memberCount.");
        }

        if (settings.AdditionalEnrollmentModulo <= 0
            || settings.TermedEnrollmentModulo <= 0
            || settings.SecondaryAddressModulo <= 0
            || settings.MiddleNameModulo <= 0
            || settings.AddressLine2Modulo <= 0)
        {
            throw new InvalidOperationException("Member seed settings modulo values must all be positive.");
        }

        if (settings.AgeRangeYears <= 0)
        {
            throw new InvalidOperationException("Member seed settings must define a positive ageRangeYears.");
        }

        if (settings.AllowedStates.Count == 0)
        {
            throw new InvalidOperationException("Member seed settings must include at least one allowed state.");
        }
    }

    private Dictionary<string, List<ZipCode>> LoadZipCodesByState(
        IReadOnlyCollection<string> allowedStates)
    {
        var zipCodes =
            jsonAssetLoader.Load<List<ZipCode>>(
                Path.Combine("referencedata", "zipcodes.json"));

        return zipCodes
            .Where(x => allowedStates.Contains(x.StateCode, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x.StateCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, List<Plan>> LoadPlansByState(
        IReadOnlyCollection<string> allowedStates)
    {
        var plans =
            jsonAssetLoader.Load<List<Plan>>(
                Path.Combine("referencedata", "plans.json"),
                jsonAssetLoader.CreateEnumJsonOptions());

        return plans
            .Where(x => allowedStates.Contains(x.StateCode, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x.StateCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private static Member CreateMember(
        int memberIndex,
        MemberSeedAssets assets)
    {
        var firstName = assets.FirstNames[(memberIndex - 1) % assets.FirstNames.Count];
        var lastName = assets.LastNames[((memberIndex - 1) * 7) % assets.LastNames.Count];
        var middleName = memberIndex % assets.Settings.MiddleNameModulo == 0
            ? ((char)('A' + ((memberIndex - 1) % 26))).ToString()
            : null;
        var createdUtc = assets.Settings.BaseCreatedUtc.AddDays(memberIndex % 30);
        var ageYears = assets.Settings.MinimumAgeYears + ((memberIndex * 7) % assets.Settings.AgeRangeYears);
        var birthDate = assets.Settings.BaseEffectiveDate.AddYears(-ageYears).AddDays(-((memberIndex * 29) % 365));

        return new Member
        {
            MemberId = CreateDeterministicGuid(0x1000_0000, memberIndex, 0),
            MemberNumber = $"PA{1_000_000 + memberIndex}",
            FirstName = firstName,
            MiddleName = middleName,
            LastName = lastName,
            DateOfBirth = birthDate,
            Gender = GetGender(memberIndex),
            EmailAddress = $"{firstName}.{lastName}.{memberIndex}@example.com".ToLowerInvariant(),
            PhoneNumber = $"555-{1000 + (memberIndex % 9000):0000}",
            IsActive = true,
            CreatedUtc = createdUtc,
            UpdatedUtc = createdUtc
        };
    }

    private MemberAddress CreateAddress(
        int memberIndex,
        int addressSequence,
        Guid memberId,
        string state,
        MemberSeedAssets assets,
        IReadOnlyDictionary<string, List<ZipCode>> zipCodesByState)
    {
        var zipCodes = zipCodesByState.GetValueOrDefault(state)
                       ?? throw new InvalidOperationException($"No zip codes found for state '{state}'.");
        var zipCode = zipCodes[((memberIndex - 1) + (addressSequence * 3)) % zipCodes.Count];
        var streetName = assets.StreetNames[((memberIndex - 1) * 5 + addressSequence) % assets.StreetNames.Count];
        var streetSuffix = assets.StreetSuffixes[((memberIndex - 1) * 3 + addressSequence) % assets.StreetSuffixes.Count];

        return new MemberAddress
        {
            MemberAddressId = CreateDeterministicGuid(0x2000_0000, memberIndex, addressSequence),
            MemberId = memberId,
            AddressLine1 = $"{100 + ((memberIndex * 13 + addressSequence * 17) % 9800)} {streetName} {streetSuffix}",
            AddressLine2 = memberIndex % assets.Settings.AddressLine2Modulo == 0
                ? assets.AddressLine2Patterns[((memberIndex - 1) + addressSequence) % assets.AddressLine2Patterns.Count]
                : null,
            City = zipCode.City,
            State = zipCode.StateCode,
            PostalCode = zipCode.PostalCode
        };
    }

    private MemberEnrollment CreateEnrollment(
        int memberIndex,
        int enrollmentSequence,
        Guid memberId,
        Guid memberAddressId,
        string state,
        MemberSeedSettings settings,
        IReadOnlyDictionary<string, List<Plan>> plansByState)
    {
        var plans = plansByState.GetValueOrDefault(state)
                    ?? throw new InvalidOperationException($"No plans found for state '{state}'.");
        var plan = plans[((memberIndex - 1) * 3 + enrollmentSequence - 1) % plans.Count];
        var effectiveDate = settings.BaseEffectiveDate.AddDays(-((memberIndex - 1) % 180) - ((enrollmentSequence - 1) * 45));
        var isTermed = memberIndex % settings.TermedEnrollmentModulo == 0 && enrollmentSequence == 1;

        return new MemberEnrollment
        {
            MemberEnrollmentId = CreateDeterministicGuid(0x3000_0000, memberIndex, enrollmentSequence),
            MemberId = memberId,
            MemberAddressId = memberAddressId,
            PlanId = plan.PlanId,
            PlanName = plan.PlanName,
            LineOfBusiness = MapLineOfBusiness(plan.LineOfBusiness),
            RelationshipToSubscriber = GetRelationship(memberIndex, enrollmentSequence),
            EffectiveDate = effectiveDate,
            TerminationDate = isTermed ? effectiveDate.AddDays(180 + (memberIndex % 90)) : null,
            IsCurrent = !isTermed
        };
    }

    private static LineOfBusiness MapLineOfBusiness(
        ReferenceLineOfBusiness lineOfBusiness)
    {
        return lineOfBusiness switch
        {
            ReferenceLineOfBusiness.Commercial => LineOfBusiness.Commercial,
            ReferenceLineOfBusiness.Medicare => LineOfBusiness.Medicare,
            ReferenceLineOfBusiness.Medicaid => LineOfBusiness.Medicaid,
            ReferenceLineOfBusiness.Exchange => LineOfBusiness.Exchange,
            _ => LineOfBusiness.Unknown
        };
    }

    private static MemberGender GetGender(int memberIndex)
    {
        return memberIndex % 6 == 0
            ? MemberGender.NonBinary
            : memberIndex % 2 == 0
                ? MemberGender.Male
                : MemberGender.Female;
    }

    private static RelationshipToSubscriber GetRelationship(
        int memberIndex,
        int enrollmentSequence)
    {
        return ((memberIndex + enrollmentSequence) % 10) switch
        {
            0 => RelationshipToSubscriber.Spouse,
            1 => RelationshipToSubscriber.Child,
            2 => RelationshipToSubscriber.OtherDependent,
            _ => RelationshipToSubscriber.Subscriber
        };
    }

    private static Guid CreateDeterministicGuid(
        int prefix,
        int primaryIndex,
        int secondaryIndex)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..4], prefix);
        BitConverter.TryWriteBytes(bytes.Slice(4, 4), primaryIndex);
        BitConverter.TryWriteBytes(bytes.Slice(8, 4), secondaryIndex);
        BitConverter.TryWriteBytes(bytes.Slice(12, 4), prefix ^ primaryIndex ^ secondaryIndex);
        return new Guid(bytes);
    }
}
