namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolCatalogBuilderTests
    : Kaleido.UnitTests.SutFixture
{
    private static ToolCatalogBuilder CreateSut(
        Func<string, string, ToolName>? naming = null,
        McpToolListing listing = McpToolListing.All,
        IReadOnlyList<string>? listedTools = null)
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

        return new ToolCatalogBuilder(
            names.Object,
            schemas.Object,
            new KaleidoMcpOptions { Instructions = "test", ToolListing = listing, ListedTools = listedTools ?? [] });
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
    public void QueryTool_WithoutProcesses_HasNoProcessId()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Queryables = [Patients()] }), "clinic_patientsource");

        Assert.False(tool.Definition.InputSchema.GetProperty("properties").TryGetProperty("processId", out _));
        Assert.DoesNotContain("processId", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryTool_WhereProcessesExist_TakesAnOptionalTopLevelProcessId()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Queryables = [Patients()], Processes = [Visits()] }), "clinic_patientsource");
        var schema = tool.Definition.InputSchema;

        Assert.Equal("uuid", schema.GetProperty("properties").GetProperty("processId").GetProperty("format").GetString());
        Assert.Equal(["parameters"], schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.False(tool.ParametersTakeProcessId);
        Assert.Contains("Pass the processId of the process you are working on, when there is one.", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryTool_WhoseSourceDeclaresAProcessId_TakesItOnlyAtTheTopLevel()
    {
        var scoped = Patients() with
        {
            Parameters = [new QueryableQueryParameter { Name = "ProcessId", Description = "The visit this search is for.", DataType = Text, Constraints = [new ConstraintContract { Type = "Required" }] }]
        };

        var tool = Tool(Build(new AggregatedRegistryResponse { Queryables = [scoped], Processes = [Visits()] }), "clinic_patientsource");
        var schema = tool.Definition.InputSchema;
        var properties = schema.GetProperty("properties");

        Assert.True(tool.ParametersTakeProcessId);
        Assert.False(properties.TryGetProperty("parameters", out _));
        Assert.StartsWith("The visit this search is for.", properties.GetProperty("processId").GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal(["processId"], schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("Scoped to one process: pass its processId.", tool.Definition.Description, StringComparison.Ordinal);
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
    public void StepTool_ByDefault_IsNeitherDestructiveNorOpenWorld_AndIdempotentUnlessRepeatable()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Processes = [Visits()] }), "clinic_startvisitstep");

        Assert.False(tool.Definition.Annotations?.DestructiveHint);
        Assert.False(tool.Definition.Annotations?.OpenWorldHint);
        Assert.True(tool.Definition.Annotations?.IdempotentHint);
        Assert.DoesNotContain("Cannot be undone.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("confirm", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void StepTool_TheDeclaredBehavior_MapsToTheAnnotationsAndTheDescription()
    {
        var processor = new ProcessorRegistryResponse
        {
            ServiceName = "billing",
            Name = "billing",
            Steps =
            [
                new ProcessStepResponse
                {
                    Name = "ChargeCardStep",
                    Description = "Charges the card",
                    IsInitial = true,
                    Repeatable = true,
                    Destructive = true,
                    External = true,
                    RequiresConfirmation = true,
                    ExecuteUrl = "/billing/processes/steps/ChargeCardStep",
                },
            ],
        };

        var tool = Tool(Build(new AggregatedRegistryResponse { Processes = [processor] }), "billing_chargecardstep");

        Assert.True(tool.Definition.Annotations?.DestructiveHint);
        Assert.True(tool.Definition.Annotations?.OpenWorldHint);
        Assert.False(tool.Definition.Annotations?.IdempotentHint);
        Assert.Contains("Cannot be undone.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("Reaches outside systems.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("Ask the user to confirm before calling.", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Tools_MarkClassifiedInputsParametersAndReturnedFieldsAsSensitive()
    {
        string[] phi = ["Clinic:PHI"];
        var visits = Visits() with
        {
            Steps =
            [
                new ProcessStepResponse
                {
                    Name = "StartVisitStep",
                    Description = "Opens a visit",
                    IsInitial = true,
                    ExecuteUrl = "/clinic/processes/steps/StartVisitStep",
                    Fields = [new ProcessFieldMetadata { Name = "PatientName", Description = "The patient's name.", DataType = Text, Classifications = phi }],
                },
            ],
        };
        var patients = Patients() with
        {
            Parameters = [new QueryableQueryParameter { Name = "Ssn", DataType = Text, Classifications = phi }],
            OutputFields = [new QueryableQueryProperty { Name = "Id", DataType = Text }, new QueryableQueryProperty { Name = "Name", DataType = Text, Classifications = phi }],
        };
        var catalog = Build(new AggregatedRegistryResponse { Queryables = [patients], Processes = [visits] });

        var step = Tool(catalog, "clinic_startvisitstep").Definition;
        var query = Tool(catalog, "clinic_patientsource").Definition;

        Assert.Equal("The patient's name. Sensitive (PHI).", step.InputSchema.GetProperty("properties").GetProperty("patientName").GetProperty("description").GetString());
        Assert.Equal("Sensitive (PHI).", query.InputSchema.GetProperty("properties").GetProperty("parameters").GetProperty("properties").GetProperty("ssn").GetProperty("description").GetString());
        Assert.Contains("Returns: Id, Name (PHI).", query.Description, StringComparison.Ordinal);
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

        Assert.Contains("Never answer on the user's behalf", Tool(catalog, ToolCatalogBuilder.AnswerInformationRequestTool).Definition.Description, StringComparison.Ordinal);
        Assert.Contains("counts only after the user confirms it", Tool(catalog, ToolCatalogBuilder.AnswerInformationRequestTool).Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_CollectsWhatHasNoDescription()
    {
        var catalog = Build(new AggregatedRegistryResponse { Queryables = [Patients()], Processes = [Visits()] });

        Assert.Contains("clinic.PatientSource.Name", catalog.MissingDescriptions);
        Assert.Contains("clinic_startvisitstep.reason", catalog.MissingDescriptions);
        Assert.DoesNotContain("clinic_startvisitstep.patientId", catalog.MissingDescriptions);
    }

    // The source and its view share the query context's fields: a missing field description is
    // reported once for the field, not once per tool.
    [Fact]
    public void Build_ReportsAMissingFieldDescriptionOnce()
    {
        var catalog = Build(new AggregatedRegistryResponse { Queryables = [Patients()] });

        Assert.Single(catalog.MissingDescriptions, x => x.EndsWith(".Name", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ExplainsSearchOnlyAndSortOnlyFields()
    {
        var patients = Patients() with
        {
            Fields =
            [
                new QueryableFieldMetadata { Name = "Id", Description = "The id.", DataType = new DataTypeDescriptor("integer"), IsFilterable = true, FilterOperators = [FilterOperator.Equals], IsSearchable = true, SearchPriority = 2, MatchMode = MatchMode.Exact },
                new QueryableFieldMetadata { Name = "Name", Description = "The patient's full name.", DataType = Text, IsSearchable = true, SearchPriority = 1, MatchMode = MatchMode.Contains },
                new QueryableFieldMetadata { Name = "Born", Description = "The date of birth.", DataType = Text, IsSortable = true },
            ],
        };

        var schema = Tool(Build(new AggregatedRegistryResponse { Queryables = [patients] }), "clinic_patientsource").Definition.InputSchema.GetProperty("properties");
        var search = schema.GetProperty("searchText").GetProperty("description").GetString();
        var sort = schema.GetProperty("sort").GetProperty("description").GetString();

        Assert.Contains("Name (contains, The patient's full name)", search, StringComparison.Ordinal);
        Assert.Contains("Id (exact)", search, StringComparison.Ordinal);
        Assert.Contains("Born (The date of birth)", sort, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_KeepsToolNamesUnique_AndRecordsTruncation()
    {
        var sut = CreateSut((service, type) => new ToolName("same", type == "CloseVisitStep"));

        var catalog = sut.Build(new AggregatedRegistryResponse { Processes = [Visits()] });
        var names = catalog.Tools.Select(x => x.Definition.Name).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("same", names);
        Assert.Contains("same_step", names);
        Assert.Contains(catalog.TruncatedNames, x => x.Contains("CloseVisitStep", StringComparison.Ordinal));
        Assert.Single(catalog.NameCollisions);
    }

    [Fact]
    public void Build_ASourceAndItsViewWithTheSameName_TheViewSaysItIsAView()
    {
        var sut = CreateSut((service, type) => new ToolName("clinic_patient", false));

        var catalog = sut.Build(new AggregatedRegistryResponse { Queryables = [Patients()] });

        Assert.Equal(["clinic_patient", "clinic_patient_view"], catalog.Tools.Select(x => x.Definition.Name));
        Assert.Contains(catalog.NameCollisions, x => x.Contains("PatientCardViewSource -> clinic_patient_view", StringComparison.Ordinal));
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

    // ── Lookups ──────────────────────────────────────────────────────────────

    private static ProcessorRegistryResponse Referrals(string viewName = "PatientCardViewSource") => new()
    {
        ServiceName = "referrals",
        Name = "referrals",
        Steps =
        [
            new ProcessStepResponse
            {
                Name = "ReferPatientStep",
                Description = "Refers a patient",
                IsInitial = true,
                ExecuteUrl = "/referrals/processes/steps/ReferPatientStep",
                Fields =
                [
                    new ProcessFieldMetadata
                    {
                        Name = "PatientId",
                        Description = "The patient.",
                        DataType = Text,
                        Lookup = new Kaleido.Processor.Registry.LookupMetadata { Service = "clinic", Source = "PatientSource", View = viewName, Field = "Id", Group = "patient", Display = ["Name", "DateOfBirth"] }
                    },
                    new ProcessFieldMetadata
                    {
                        Name = "PatientName",
                        Description = "The patient's name.",
                        DataType = Text,
                        Lookup = new Kaleido.Processor.Registry.LookupMetadata { Service = "clinic", Source = "PatientSource", View = viewName, Field = "Name", Group = "patient" }
                    },
                    new ProcessFieldMetadata { Name = "Reason", Description = "Why.", DataType = Text }
                ]
            }
        ]
    };

    // ── Front door ───────────────────────────────────────────────────────────

    private static ToolCatalog FrontDoor(params string[] listedTools) =>
        CreateSut(listing: McpToolListing.FrontDoor, listedTools: listedTools)
            .Build(new AggregatedRegistryResponse { Queryables = [Patients()], Processes = [Visits(), Referrals()] });

    private static string[] Listed(ToolCatalog catalog) =>
        [.. catalog.Tools.Where(x => x.Listed).Select(x => x.Definition.Name)];

    [Fact]
    public void All_ListsEveryTool_WithoutTheFrontDoorBuiltIns()
    {
        var catalog = Build(new AggregatedRegistryResponse { Queryables = [Patients()], Processes = [Visits()] });

        Assert.All(catalog.Tools, x => Assert.True(x.Listed));
        Assert.False(catalog.IsFrontDoor);
        Assert.Null(catalog.Find(ToolCatalogBuilder.CallToolTool));
    }

    [Fact]
    public void FrontDoor_ListsTheStartingSteps_TheirLookupQueries_AndTheBuiltIns()
    {
        var catalog = FrontDoor();

        Assert.True(catalog.IsFrontDoor);
        Assert.Equal(
            [
                "clinic_patientcardviewsource",
                "clinic_startvisitstep",
                "referrals_referpatientstep",
                "answer_information_request",
                "get_process_state",
                "search_tools",
                "describe_tool",
                "call_tool",
            ],
            Listed(catalog));
    }

    [Fact]
    public void FrontDoor_KeepsEveryOtherTool_UnlistedButCallable()
    {
        var catalog = FrontDoor();

        Assert.False(Tool(catalog, "clinic_closevisitstep").Listed);
        Assert.False(Tool(catalog, "clinic_patientsource").Listed);
    }

    [Fact]
    public void FrontDoor_ListsTheConfiguredExtras_AndReportsUnknownNames()
    {
        var catalog = FrontDoor("clinic_patientsource", "clinic_nothing");

        Assert.True(Tool(catalog, "clinic_patientsource").Listed);
        Assert.Equal(["clinic_nothing"], catalog.UnknownListedTools);
    }

    [Fact]
    public void FrontDoor_CallTool_KeepsClientsAskingBeforeEachCall()
    {
        var annotations = Tool(FrontDoor(), ToolCatalogBuilder.CallToolTool).Definition.Annotations;

        Assert.False(annotations?.ReadOnlyHint);
        Assert.True(annotations?.DestructiveHint);
    }

    private static string ArgumentDescription(KaleidoTool tool, string argument) =>
        tool.Definition.InputSchema.GetProperty("properties").GetProperty(argument).GetProperty("description").GetString() ?? string.Empty;

    [Fact]
    public void StepTool_ALookup_NamesTheQueryTool_TheResultField_AndTheInputsFromTheSameResult()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Queryables = [Patients()], Processes = [Referrals()] }), "referrals_referpatientstep");

        var patientId = ArgumentDescription(tool, "patientId");

        Assert.StartsWith("The patient.", patientId, StringComparison.Ordinal);
        Assert.Contains("the Id of one Patient card result", patientId, StringComparison.Ordinal);
        Assert.Contains("search with clinic_patientcardviewsource", patientId, StringComparison.Ordinal);
        Assert.Contains("(Name, DateOfBirth)", patientId, StringComparison.Ordinal);
        Assert.Contains("never guess", patientId, StringComparison.Ordinal);
        Assert.Contains("Take patientName from the same result.", patientId, StringComparison.Ordinal);
        Assert.DoesNotContain("Value:", ArgumentDescription(tool, "reason"), StringComparison.Ordinal);
        Assert.Contains("Before calling, find patientId and patientName with clinic_patientcardviewsource", tool.Definition.Description, StringComparison.Ordinal);

        var lookup = Assert.Single(tool.Lookups);
        Assert.Equal("clinic_patientcardviewsource", lookup.QueryTool);
        Assert.Equal([("patientId", "Id"), ("patientName", "Name")], lookup.Inputs);
    }

    [Fact]
    public void StepTool_ALookupTheCallerCannotSearch_SaysToAskTheUser_AndIsReported()
    {
        // No clinic queryables: the caller cannot see the lookup's query.
        var catalog = Build(new AggregatedRegistryResponse { Processes = [Referrals()] });
        var tool = Tool(catalog, "referrals_referpatientstep");

        Assert.Contains("which you cannot search: ask the user", ArgumentDescription(tool, "patientId"), StringComparison.Ordinal);
        Assert.Null(Assert.Single(tool.Lookups).QueryTool);
        Assert.Contains("referrals_referpatientstep.patientId -> clinic/PatientSource/PatientCardViewSource", catalog.UnresolvedLookups);
    }

    [Fact]
    public void Build_WithoutARegistry_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().Build(null!));
    }

    [Fact]
    public void Build_AProcessorThatPublishesNoSteps_HasNoStepTools_AndNoBuiltIns()
    {
        var catalog = Build(new AggregatedRegistryResponse
        {
            Processes = [new ProcessorRegistryResponse { ServiceName = "clinic", Name = "clinic", Steps = null }]
        });

        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.ProcessServices);
    }

    [Fact]
    public void QueryTool_OfAnUndescribedSource_FallsBackToItsName_AndIsReported()
    {
        var bare = new QueryableSourceResponse
        {
            ServiceName = "clinic",
            Name = "PatientSource",
            QueryUrl = "/clinic/queryable/patients/query",
            Fields = [new QueryableFieldMetadata { Name = "Name", Description = "The name.", DataType = Text, IsSearchable = true }],
            Views = [new QueryableViewResponse { Name = "PatientCardViewSource", QueryUrl = "/clinic/queryable/patients/views/card/query" }]
        };

        var catalog = Build(new AggregatedRegistryResponse { Queryables = [bare] });
        var source = Tool(catalog, "clinic_patientsource").Definition;
        var view = Tool(catalog, "clinic_patientcardviewsource").Definition;

        Assert.Equal("PatientSource", source.Title);
        Assert.Equal(". Read-only.", source.Description);
        Assert.Equal("PatientCardViewSource", view.Title);
        Assert.Equal(". A view of PatientSource. Read-only.", view.Description);
        Assert.Equal(["clinic_patientsource", "clinic_patientcardviewsource"], catalog.MissingDescriptions);
        Assert.Contains("Name (contains, The name)", source.InputSchema.GetProperty("properties").GetProperty("searchText").GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.False(source.InputSchema.GetProperty("properties").TryGetProperty("page", out _));
        Assert.False(source.InputSchema.TryGetProperty("required", out _));
    }

    [Fact]
    public void QueryTool_FiltersDescribeEnumsByTheirValues_AndTypesByTheirFormat()
    {
        var patients = Patients() with
        {
            Fields =
            [
                new QueryableFieldMetadata
                {
                    Name = "Kind",
                    DataType = new DataTypeDescriptor("string", EnumValues: [new EnumValueDescriptor(0, "Outpatient", null), new EnumValueDescriptor(1, "Inpatient", null)]),
                    IsFilterable = true,
                    FilterOperators = [FilterOperator.Equals]
                },
                new QueryableFieldMetadata { Name = "Born", Description = "Date of birth.", DataType = new DataTypeDescriptor("string", "date"), IsFilterable = true, FilterOperators = [FilterOperator.Between] },
                new QueryableFieldMetadata { Name = "Ignored", DataType = Text, IsFilterable = true, FilterOperators = [] }
            ]
        };

        var filters = Tool(Build(new AggregatedRegistryResponse { Queryables = [patients] }), "clinic_patientsource")
            .Definition.InputSchema.GetProperty("properties").GetProperty("filters");

        Assert.Equal("Conditions, all of which must match. Fields and their operators: Kind (outpatient|inpatient): equals; Born (date, Date of birth): between. Pass values typed like the field (in/between take several values; isNull/isTrue take none).", filters.GetProperty("description").GetString());
        Assert.Equal(["Kind", "Born"], filters.GetProperty("items").GetProperty("properties").GetProperty("field").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void QueryTool_ListsAtMostFifteenReturnedFields()
    {
        var patients = Patients() with
        {
            OutputFields = [.. Enumerable.Range(1, 16).Select(i => new QueryableQueryProperty { Name = $"F{i}", DataType = Text })]
        };

        var description = Tool(Build(new AggregatedRegistryResponse { Queryables = [patients] }), "clinic_patientsource").Definition.Description;

        Assert.Contains($"Returns: {string.Join(", ", Enumerable.Range(1, 15).Select(i => $"F{i}"))}, ….", description, StringComparison.Ordinal);
        Assert.DoesNotContain("F16", description, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryTool_WhoseSourceDeclaresAnOptionalUndescribedProcessId_TakesItWithoutProcesses()
    {
        var scoped = Patients() with
        {
            Parameters = [new QueryableQueryParameter { Name = "processId", DataType = Text }]
        };

        var tool = Tool(Build(new AggregatedRegistryResponse { Queryables = [scoped] }), "clinic_patientsource");
        var properties = tool.Definition.InputSchema.GetProperty("properties");

        Assert.True(tool.ParametersTakeProcessId);
        Assert.Equal(
            "The process this query is for (returned by an earlier step). Also sent with the query so it is recorded with the process.",
            properties.GetProperty("processId").GetProperty("description").GetString());
        Assert.False(tool.Definition.InputSchema.TryGetProperty("required", out _));
        Assert.Contains("Pass the processId of the process you are working on, when there is one.", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryTool_AViewsOptionalParameters_AreNotRequired()
    {
        var patients = Patients() with
        {
            Views =
            [
                new QueryableViewResponse
                {
                    Name = "PatientCardViewSource",
                    Description = "A card.",
                    QueryUrl = "/clinic/queryable/patients/views/card/query",
                    Parameters = [new QueryableQueryParameter { Name = "Since", Description = "From this date.", DataType = Text }]
                }
            ]
        };

        var schema = Tool(Build(new AggregatedRegistryResponse { Queryables = [patients] }), "clinic_patientcardviewsource").Definition.InputSchema;

        Assert.False(schema.TryGetProperty("required", out _));
        Assert.False(schema.GetProperty("properties").GetProperty("parameters").TryGetProperty("required", out _));
        Assert.Equal("From this date.", schema.GetProperty("properties").GetProperty("parameters").GetProperty("properties").GetProperty("since").GetProperty("description").GetString());
    }

    [Fact]
    public void StepTool_OfAnUndescribedStep_FallsBackToItsName_IsReported_AndListsWhatClosesIt()
    {
        var processor = new ProcessorRegistryResponse
        {
            ServiceName = "clinic",
            Name = "clinic",
            Steps =
            [
                new ProcessStepResponse
                {
                    Name = "WaitStep",
                    ExecuteUrl = "/clinic/processes/steps/WaitStep",
                    AvailableUntil = [new ProcessStepSummary { Name = "CloseVisitStep" }, new ProcessStepSummary { Name = "CancelVisitStep" }]
                }
            ]
        };

        var catalog = Build(new AggregatedRegistryResponse { Processes = [processor] });
        var tool = Tool(catalog, "clinic_waitstep");

        Assert.Equal("WaitStep", tool.Definition.Title);
        Assert.Equal("WaitStep", tool.Definition.Annotations?.Title);
        Assert.StartsWith(". Continues an existing process: pass its processId. Changes state.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.Equal(["CloseVisitStep", "CancelVisitStep"], tool.ClosedBy);
        Assert.Contains("clinic_waitstep", catalog.MissingDescriptions);
    }

    [Fact]
    public void Build_ManyCapabilitiesDerivingOneName_AreNumbered_AndEachIsReported()
    {
        var processor = Visits() with
        {
            Steps =
            [
                new ProcessStepResponse { Name = "AStep", Description = "A.", ExecuteUrl = "/a" },
                new ProcessStepResponse { Name = "BStep", Description = "B.", ExecuteUrl = "/b" },
                new ProcessStepResponse { Name = "CStep", Description = "C.", ExecuteUrl = "/c" },
                new ProcessStepResponse { Name = "DStep", Description = "D.", ExecuteUrl = "/d" }
            ]
        };

        var catalog = CreateSut((_, _) => new ToolName("same", false)).Build(new AggregatedRegistryResponse { Processes = [processor] });

        Assert.Equal(["same", "same_step", "same_step_2", "same_step_3"], catalog.Tools.Where(x => x.Kind == KaleidoToolKind.Step).Select(x => x.Definition.Name));
        Assert.Equal(
            [
                "clinic/BStep -> same_step (derives same, already used)",
                "clinic/CStep -> same_step_2 (derives same, already used)",
                "clinic/DStep -> same_step_3 (derives same, already used)"
            ],
            catalog.NameCollisions);
    }

    [Fact]
    public void Build_ACollisionAtTheLengthLimit_KeepsTheNameWithinIt()
    {
        var longName = new string('x', ToolNameBuilder.MaxLength);

        var catalog = CreateSut((_, _) => new ToolName(longName, false)).Build(new AggregatedRegistryResponse { Queryables = [Patients()] });

        var names = catalog.Tools.Select(x => x.Definition.Name).ToArray();
        Assert.Equal([longName, new string('x', ToolNameBuilder.MaxLength - 5) + "_view"], names);
        Assert.All(names, x => Assert.True(x.Length <= ToolNameBuilder.MaxLength));
    }

    [Fact]
    public void StepTool_ALookupOfASource_FindsItsQueryTool_AmongToolsOfOtherServicesSourcesAndViews()
    {
        var appointments = new QueryableSourceResponse { ServiceName = "clinic", Name = "AppointmentSource", Description = "Appointments.", QueryUrl = "/clinic/queryable/appointments/query" };
        var billingPatients = Patients() with { ServiceName = "billing", QueryUrl = "/billing/queryable/patients/query", Views = [] };
        var alpha = new ProcessorRegistryResponse
        {
            ServiceName = "alpha",
            Name = "alpha",
            Steps = [new ProcessStepResponse { Name = "PingStep", Description = "Pings.", IsInitial = true, ExecuteUrl = "/alpha/processes/steps/PingStep" }]
        };
        var referrals = new ProcessorRegistryResponse
        {
            ServiceName = "referrals",
            Name = "referrals",
            Steps =
            [
                new ProcessStepResponse
                {
                    Name = "ReferPatientStep",
                    Description = "Refers a patient",
                    ExecuteUrl = "/referrals/processes/steps/ReferPatientStep",
                    Fields =
                    [
                        new ProcessFieldMetadata
                        {
                            Name = "PatientId",
                            Description = "The patient.",
                            DataType = Text,
                            Lookup = new Kaleido.Processor.Registry.LookupMetadata { Service = "clinic", Source = "PatientSource", Field = "Id", Group = "patient" }
                        }
                    ]
                }
            ]
        };

        var tool = Tool(
            Build(new AggregatedRegistryResponse { Queryables = [appointments, billingPatients, Patients()], Processes = [alpha, referrals] }),
            "referrals_referpatientstep");

        var lookup = Assert.Single(tool.Lookups);
        Assert.Equal("clinic_patientsource", lookup.QueryTool);
        Assert.Equal("Patients", lookup.QueryTitle);
        Assert.Null(lookup.View);
        Assert.Empty(lookup.Display);
        Assert.Equal(
            "The patient. Value: the Id of one Patients result. Ask the user for details that identify the record, search with clinic_patientsource, show the matches and let the user pick one; never guess or invent it.",
            ArgumentDescription(tool, "patientId"));
    }

    [Fact]
    public void StepTool_ALookupOfASourceTheCallerCannotSearch_NamesTheSource()
    {
        var referrals = Referrals() with
        {
            Steps =
            [
                new ProcessStepResponse
                {
                    Name = "ReferPatientStep",
                    Description = "Refers a patient",
                    IsInitial = true,
                    ExecuteUrl = "/referrals/processes/steps/ReferPatientStep",
                    Fields =
                    [
                        new ProcessFieldMetadata
                        {
                            Name = "PatientId",
                            DataType = Text,
                            Lookup = new Kaleido.Processor.Registry.LookupMetadata { Service = "clinic", Source = "PatientSource", Field = "Id", Group = "patient" }
                        }
                    ]
                }
            ]
        };

        var catalog = Build(new AggregatedRegistryResponse { Processes = [Visits(), referrals] });
        var tool = Tool(catalog, "referrals_referpatientstep");

        Assert.Equal(["referrals_referpatientstep.patientId -> clinic/PatientSource"], catalog.UnresolvedLookups);
        Assert.Equal("Value: a PatientSource result's Id on clinic, which you cannot search: ask the user.", ArgumentDescription(tool, "patientId"));
        Assert.Contains(" patientId come from PatientSource on clinic, which you cannot search: ask the user.", tool.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void StepTool_ALookupThroughAViewTheCallerCannotSearch_NamesTheView()
    {
        var tool = Tool(Build(new AggregatedRegistryResponse { Processes = [Referrals()] }), "referrals_referpatientstep");

        Assert.Contains(" patientId and patientName come from PatientCardViewSource on clinic, which you cannot search: ask the user.", tool.Definition.Description, StringComparison.Ordinal);
        Assert.StartsWith("The patient. Value: a PatientCardViewSource result's Id on clinic", ArgumentDescription(tool, "patientId"), StringComparison.Ordinal);
    }

    [Fact]
    public void Tools_MarkClassifiedReturnedStepFields_AndDescribedClassifiedParameters()
    {
        string[] pii = ["Clinic:PII", "PHI"];
        var visits = Visits() with
        {
            Steps =
            [
                new ProcessStepResponse
                {
                    Name = "StartVisitStep",
                    Description = "Opens a visit.",
                    IsInitial = true,
                    ExecuteUrl = "/clinic/processes/steps/StartVisitStep",
                    Fields = [new ProcessFieldMetadata { Name = "Phone", DataType = Text, Classifications = pii }],
                    Result = new ProcessStepResultMetadata { OutputFields = [new ProcessOutputFieldMetadata { Name = "VisitId", DataType = Text }, new ProcessOutputFieldMetadata { Name = "Phone", DataType = Text, Classifications = pii }] }
                }
            ]
        };
        var patients = Patients() with
        {
            Parameters = [new QueryableQueryParameter { Name = "Ssn", Description = " The SSN. ", DataType = Text, Classifications = pii }]
        };

        var catalog = Build(new AggregatedRegistryResponse { Queryables = [patients], Processes = [visits] });
        var step = Tool(catalog, "clinic_startvisitstep").Definition;
        var query = Tool(catalog, "clinic_patientsource").Definition;

        Assert.Contains("Returns: VisitId, Phone (PII, PHI).", step.Description, StringComparison.Ordinal);
        Assert.Equal("Sensitive (PII, PHI).", step.InputSchema.GetProperty("properties").GetProperty("phone").GetProperty("description").GetString());
        Assert.Equal("The SSN. Sensitive (PII, PHI).", query.InputSchema.GetProperty("properties").GetProperty("parameters").GetProperty("properties").GetProperty("ssn").GetProperty("description").GetString());
    }

    [Fact]
    public void StepTool_AFieldWithoutAnyHint_HasNoDescription()
    {
        var visits = Visits() with
        {
            Steps = [new ProcessStepResponse { Name = "NoteStep", Description = "Adds a note.", IsInitial = true, ExecuteUrl = "/clinic/processes/steps/NoteStep", Fields = [new ProcessFieldMetadata { Name = "Note", DataType = Text }] }]
        };

        var tool = Tool(Build(new AggregatedRegistryResponse { Processes = [visits] }), "clinic_notestep");

        Assert.False(tool.Definition.InputSchema.GetProperty("properties").GetProperty("note").TryGetProperty("description", out _));
        Assert.False(tool.Definition.InputSchema.TryGetProperty("required", out _));
    }

    [Fact]
    public void StepTool_ALookupThatIsNotListedAtTheFrontDoor_StaysUnlisted_WhenUnresolved()
    {
        var catalog = CreateSut(listing: McpToolListing.FrontDoor).Build(new AggregatedRegistryResponse { Processes = [Referrals()] });

        Assert.True(Tool(catalog, "referrals_referpatientstep").Listed);
        Assert.Empty(catalog.UnknownListedTools);
        Assert.Equal(2, catalog.UnresolvedLookups.Count);
    }

    [Fact]
    public void StepTool_ALookupOfAnUnknownView_IsUnresolved()
    {
        var catalog = Build(new AggregatedRegistryResponse { Queryables = [Patients()], Processes = [Referrals("NoSuchView")] });

        Assert.Equal(2, catalog.UnresolvedLookups.Count);
    }
}
