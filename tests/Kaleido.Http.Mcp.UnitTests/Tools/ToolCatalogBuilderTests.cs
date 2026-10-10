namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolCatalogBuilderTests
    : Kaleido.UnitTests.SutFixture
{
    private static ToolCatalogBuilder CreateSut(Func<string, string, ToolName>? naming = null)
    {
        var names = new Mock<IToolNameBuilder>();
        names.Setup(x => x.Build(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string service, string type) => naming?.Invoke(service, type) ?? new ToolName($"{service}_{type}".ToLowerInvariant(), false));

        var schemas = new Mock<IToolSchemaBuilder>();
        schemas.Setup(x => x.ForValue(It.IsAny<DataTypeDescriptor>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<ConstraintContract>?>()))
            .Returns((DataTypeDescriptor type, string? description, IReadOnlyCollection<ConstraintContract>? _) =>
                description is null
                    ? new JsonObject { ["type"] = type.Type }
                    : new JsonObject { ["type"] = type.Type, ["description"] = description });
        schemas.Setup(x => x.IsRequired(It.IsAny<IReadOnlyCollection<ConstraintContract>?>()))
            .Returns((IReadOnlyCollection<ConstraintContract>? constraints) => constraints?.Any(c => c.Type == "Required") == true);

        return new ToolCatalogBuilder(names.Object, schemas.Object);
    }

    private static readonly DataTypeDescriptor Text = new("string");

    private static QueryableSourceResponse Patients(string? queryUrl = "/clinic/queryable/patients/query") => new()
    {
        ServiceName = "clinic",
        Name = "PatientSource",
        DisplayName = "Patients",
        Description = "Patients registered at the clinic",
        Source = "EHR",
        QueryUrl = queryUrl,
        Pageable = new PageableMetadata(25, 100),
        Fields =
        [
            new QueryableFieldMetadata { Name = "Id", Description = "The id.", DataType = new DataTypeDescriptor("integer"), IsFilterable = true, FilterOperators = [FilterOperator.Equals, FilterOperator.In] },
            new QueryableFieldMetadata { Name = "Name", DataType = Text, IsSearchable = true, SearchPriority = 1, MatchMode = MatchMode.Contains, IsSortable = true }
        ],
        Parameters = [new QueryableQueryParameter { Name = "ClinicId", Description = "The clinic.", DataType = Text, Constraints = [new ConstraintContract { Type = "Required" }] }],
        OutputFields = [new QueryableQueryProperty { Name = "Id", DataType = Text }, new QueryableQueryProperty { Name = "Name", DataType = Text }],
        Views =
        [
            new QueryableViewResponse { Name = "PatientCardViewSource", DisplayName = "Patient card", Description = "A card.", QueryUrl = "/clinic/queryable/patients/views/card/query" }
        ]
    };

    private static ProcessorRegistryResponse Visits() => new()
    {
        ServiceName = "clinic",
        Name = "clinic",
        Steps =
        [
            new ProcessStepResponse
            {
                Name = "StartVisitStep",
                DisplayName = "Start visit",
                Description = "Opens a visit",
                IsInitial = true,
                ExecuteUrl = "/clinic/processes/steps/StartVisitStep",
                Fields =
                [
                    new ProcessFieldMetadata { Name = "PatientId", Description = "The patient.", DisplayName = "Patient", Prompt = "Which patient?", DataType = new DataTypeDescriptor("integer"), Constraints = [new ConstraintContract { Type = "Required" }] },
                    new ProcessFieldMetadata { Name = "Reason", DataType = Text }
                ],
                Result = new ProcessStepResultMetadata { OutputFields = [new ProcessOutputFieldMetadata { Name = "VisitId", DataType = Text }] }
            },
            new ProcessStepResponse { Name = "VisitQuestionsStep", IsInformationStep = true, ExecuteUrl = "/clinic/processes/steps/VisitQuestionsStep" },
            new ProcessStepResponse { Name = "CloseVisitStep", Description = "Closes the visit", ExecuteUrl = "/clinic/processes/steps/CloseVisitStep" }
        ]
    };

    private static ToolCatalog Build(AggregatedRegistryResponse registry) => CreateSut().Build(registry);

    private static KaleidoTool Tool(ToolCatalog catalog, string name) =>
        catalog.Find(name) ?? throw new Xunit.Sdk.XunitException($"No tool {name}; have {string.Join(", ", catalog.Tools.Select(x => x.Definition.Name))}");

    [Fact]
    public void Build_MakesQueryToolsForSourcesAndViews_StepToolsForOrdinarySteps_AndTheBuiltIns()
    {
        var catalog = Build(new AggregatedRegistryResponse { Revision = "r7", Queryables = [Patients()], Processes = [Visits()] });

        Assert.Equal(
            ["clinic_patientsource", "clinic_patientcardviewsource", "clinic_closevisitstep", "clinic_startvisitstep", "answer_information_request", "get_process_state"],
            catalog.Tools.Select(x => x.Definition.Name));
        Assert.Equal("r7", catalog.Revision);
        Assert.Equal(["clinic"], catalog.ProcessServices);
    }

    [Fact]
    public void Build_InformationSteps_AreNeverTools()
    {
        var catalog = Build(new AggregatedRegistryResponse { Processes = [Visits()] });

        Assert.Null(catalog.Find("clinic_visitquestionsstep"));
        Assert.Null(catalog.FindByUrl("/clinic/processes/steps/VisitQuestionsStep"));
    }

    [Fact]
    public void Build_WithoutProcesses_HasNoBuiltInTools()
    {
        var catalog = Build(new AggregatedRegistryResponse { Queryables = [Patients()] });

        Assert.Null(catalog.Find(ToolCatalogBuilder.AnswerInformationRequestTool));
        Assert.Null(catalog.Find(ToolCatalogBuilder.GetProcessStateTool));
    }

    [Fact]
    public void Build_ASourceWithoutAQueryUrl_HasOnlyItsViews()
    {
        var catalog = Build(new AggregatedRegistryResponse { Queryables = [Patients(queryUrl: null)] });

        Assert.Null(catalog.Find("clinic_patientsource"));
        Assert.NotNull(catalog.Find("clinic_patientcardviewsource"));
    }

    [Fact]
    public void QueryTool_IsReadOnly_Described_AndTargetsTheQueryUrl()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Queryables = [Patients()] }), "clinic_patientsource");

        Assert.Equal(KaleidoToolKind.Query, tool.Kind);
        Assert.Equal("/clinic/queryable/patients/query", tool.Url);
        Assert.True(tool.Definition.Annotations?.ReadOnlyHint);
        Assert.True(tool.Definition.Annotations?.IdempotentHint);
        Assert.StartsWith("Patients: Patients registered at the clinic.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("Data source: EHR.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("Returns: Id, Name.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.EndsWith("Read-only.", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryTool_SchemaOffersOnlyWhatTheSourceSupports()
    {
        var properties = Tool(Build(new AggregatedRegistryResponse { Queryables = [Patients()] }), "clinic_patientsource")
            .Definition.InputSchema.GetProperty("properties");

        Assert.Contains("Name (contains)", properties.GetProperty("searchText").GetProperty("description").GetString(), StringComparison.Ordinal);

        var filters = properties.GetProperty("filters");
        Assert.Contains("Id (integer, The id): equals, in", filters.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal(["Id"], filters.GetProperty("items").GetProperty("properties").GetProperty("field").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));

        Assert.Equal(["Name"], properties.GetProperty("sort").GetProperty("items").GetProperty("properties").GetProperty("field").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(100, properties.GetProperty("page").GetProperty("properties").GetProperty("size").GetProperty("maximum").GetInt32());
        Assert.Equal(["clinicId"], properties.GetProperty("parameters").GetProperty("required").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void QueryTool_WithARequiredParameter_RequiresParameters()
    {
        var schema = Tool(Build(new AggregatedRegistryResponse { Queryables = [Patients()] }), "clinic_patientsource").Definition.InputSchema;

        Assert.Equal(["parameters"], schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void ViewTool_UsesItsSourcesFields_AndNamesItsSource()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Queryables = [Patients()] }), "clinic_patientcardviewsource");

        Assert.Equal("/clinic/queryable/patients/views/card/query", tool.Url);
        Assert.Contains("A view of Patients.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.True(tool.Definition.InputSchema.GetProperty("properties").TryGetProperty("searchText", out _));
    }

    [Fact]
    public void StepTool_ChangesState_TakesAProcessId_AndTheStepsFields()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Processes = [Visits()] }), "clinic_startvisitstep");
        var schema = tool.Definition.InputSchema;
        var properties = schema.GetProperty("properties");

        Assert.Equal(KaleidoToolKind.Step, tool.Kind);
        Assert.True(tool.IsInitial);
        Assert.False(tool.Definition.Annotations?.ReadOnlyHint);
        Assert.Equal("uuid", properties.GetProperty("processId").GetProperty("format").GetString());
        Assert.Equal("The patient. Label: Patient. Ask: Which patient?", properties.GetProperty("patientId").GetProperty("description").GetString());
        Assert.Equal(["patientId"], schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("Can start a new process", tool.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("Returns: VisitId.", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void StepTool_ThatIsNotInitial_RequiresTheProcessId()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Processes = [Visits()] }), "clinic_closevisitstep");

        Assert.Equal(["processId"], tool.Definition.InputSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("pass its processId", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInTools_OfferTheServicesThatHaveProcesses()
    {
        var catalog = Build(new AggregatedRegistryResponse { Processes = [Visits()] });

        foreach (var name in new[] { ToolCatalogBuilder.AnswerInformationRequestTool, ToolCatalogBuilder.GetProcessStateTool })
        {
            var service = Tool(catalog, name).Definition.InputSchema.GetProperty("properties").GetProperty("service");

            Assert.Equal(["clinic"], service.GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        }

        Assert.Contains("never answer on the user's behalf", Tool(catalog, ToolCatalogBuilder.AnswerInformationRequestTool).Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_CollectsWhatHasNoDescription()
    {
        var catalog = Build(new AggregatedRegistryResponse { Queryables = [Patients()], Processes = [Visits()] });

        Assert.Contains("clinic_patientsource.Name", catalog.MissingDescriptions);
        Assert.Contains("clinic_startvisitstep.reason", catalog.MissingDescriptions);
        Assert.DoesNotContain("clinic_startvisitstep.patientId", catalog.MissingDescriptions);
    }

    [Fact]
    public void Build_KeepsToolNamesUnique_AndRecordsTruncation()
    {
        var sut = CreateSut((service, type) => new ToolName("same", type == "CloseVisitStep"));

        var catalog = sut.Build(new AggregatedRegistryResponse { Processes = [Visits()] });
        var names = catalog.Tools.Select(x => x.Definition.Name).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("same", names);
        Assert.Contains("same_2", names);
        Assert.Contains(catalog.TruncatedNames, x => x.Contains("CloseVisitStep", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_APartialRegistry_IsMarked_WithTheServicesThatFailed()
    {
        var catalog = Build(new AggregatedRegistryResponse
        {
            IsPartial = true,
            ClientErrors = [new RegistryClientError { ClientName = "radiology", ClientType = "processor", Reason = "timeout" }]
        });

        Assert.True(catalog.IsPartial);
        Assert.Equal(["radiology"], catalog.UnavailableServices);
    }

    [Fact]
    public void FindByUrl_MapsAnExecuteUrlBackToItsTool_CaseInsensitively()
    {
        var catalog = Build(new AggregatedRegistryResponse { Processes = [Visits()] });

        Assert.Equal("clinic_closevisitstep", catalog.FindByUrl("/CLINIC/processes/steps/CloseVisitStep")?.Definition.Name);
        Assert.Null(catalog.FindByUrl(null));
    }
}
