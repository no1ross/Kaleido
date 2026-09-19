//using Kaleido.Process.Attributes;
//using Kaleido.Process.Processor;
//using Kaleido.Process.Processor.Execution;
//using Kaleido.Process.Processor.Planning;
//using Kaleido.Process.Processor.Registry;
//using Moq;
//using Xunit;

//namespace Kaleido.Process.UnitTests.Processor.Planning;

//public sealed class StepCandidateBuilderTests
//{
//    [Fact]
//    public void Constructor_WhenRegistryIsNull_Throws()
//    {
//        var exception =
//            Assert.Throws<ArgumentNullException>(() =>
//                new StepCandidateBuilder(null!));

//        Assert.Equal("registry", exception.ParamName);
//    }

//    [Fact]
//    public void Build_WhenRequestIsNull_Throws()
//    {
//        var builder =
//            new StepCandidateBuilder(
//                Mock.Of<IProcessStepRegistry>());

//        var exception =
//            Assert.Throws<ArgumentNullException>(() =>
//                builder.Build(null!));

//        Assert.Equal("request", exception.ParamName);
//    }

//    [Fact]
//    public void Build_WhenStepIsNotRegistered_ReturnsInvalidCandidate()
//    {
//        var registry =
//            new Mock<IProcessStepRegistry>(MockBehavior.Strict);

//        registry
//            .Setup(x => x.Find("missing-step"))
//            .Returns((ProcessStepRegistration?)null);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["missing-step"] =
//                        new Dictionary<string, object?>()
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal("missing-step", candidate.StepName);
//        Assert.Null(candidate.Registration);
//        Assert.Null(candidate.Step);
//        Assert.True(candidate.HasErrors);

//        Assert.Contains(
//            candidate.Messages,
//            x => x.Code == StepProcessingMessageCode.UnknownStep);

//        registry.Verify(
//            x => x.Find("missing-step"),
//            Times.Once);
//    }

//    [Fact]
//    public void Build_WhenStepIsRegistered_BuildsSimpleClassStep()
//    {
//        var registration =
//            CreateRegistration<SimpleClassStep, SimpleClassStepResult, SimpleClassStepHandler>(
//                "simple-class");

//        var registry =
//            CreateRegistry(
//                "simple-class",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["simple-class"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Name"] = "Andrew",
//                            ["Quantity"] = 42,
//                            ["IsActive"] = true
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal("simple-class", candidate.StepName);
//        Assert.Same(registration, candidate.Registration);
//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<SimpleClassStep>(candidate.Step);

//        Assert.Equal("Andrew", step.Name);
//        Assert.Equal(42, step.Quantity);
//        Assert.True(step.IsActive);
//    }

//    [Fact]
//    public void Build_WhenStepUsesCamelCaseProperties_BuildsStepCaseInsensitively()
//    {
//        var registration =
//            CreateRegistration<SimpleClassStep, SimpleClassStepResult, SimpleClassStepHandler>(
//                "simple-class");

//        var registry =
//            CreateRegistry(
//                "simple-class",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["simple-class"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["name"] = "Andrew",
//                            ["quantity"] = 42,
//                            ["isActive"] = true
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<SimpleClassStep>(candidate.Step);

//        Assert.Equal("Andrew", step.Name);
//        Assert.Equal(42, step.Quantity);
//        Assert.True(step.IsActive);
//    }

//    [Fact]
//    public void Build_WhenStepUsesInitProperties_BuildsStep()
//    {
//        var registration =
//            CreateRegistration<InitPropertyStep, InitPropertyStepResult, InitPropertyStepHandler>(
//                "init-property");

//        var registry =
//            CreateRegistry(
//                "init-property",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["init-property"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["RequestId"] = "REQ-123",
//                            ["Amount"] = "19.95"
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<InitPropertyStep>(candidate.Step);

//        Assert.Equal("REQ-123", step.RequestId);
//        Assert.Equal(19.95m, step.Amount);
//    }

//    [Fact]
//    public void Build_WhenStepIsRecordWithPrimaryConstructor_BuildsRecord()
//    {
//        var registration =
//            CreateRegistration<RecordStep, RecordStepResult, RecordStepHandler>(
//                "record-step");

//        var registry =
//            CreateRegistry(
//                "record-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var customerId =
//            Guid.NewGuid();

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["record-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["CustomerId"] = customerId,
//                            ["Name"] = "Andrew"
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<RecordStep>(candidate.Step);

//        Assert.Equal(customerId, step.CustomerId);
//        Assert.Equal("Andrew", step.Name);
//    }

//    [Fact]
//    public void Build_WhenStepContainsEnum_BuildsEnumFromCamelCaseString()
//    {
//        var registration =
//            CreateRegistration<EnumStep, EnumStepResult, EnumStepHandler>(
//                "enum-step");

//        var registry =
//            CreateRegistry(
//                "enum-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["enum-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Status"] = "waitingForApproval"
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<EnumStep>(candidate.Step);

//        Assert.Equal(TestStepStatus.WaitingForApproval, step.Status);
//    }

//    [Fact]
//    public void Build_WhenStepContainsNullableEnum_BuildsNullableEnumFromString()
//    {
//        var registration =
//            CreateRegistration<NullableEnumStep, NullableEnumStepResult, NullableEnumStepHandler>(
//                "nullable-enum-step");

//        var registry =
//            CreateRegistry(
//                "nullable-enum-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["nullable-enum-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Status"] = "completed"
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<NullableEnumStep>(candidate.Step);

//        Assert.Equal(TestStepStatus.Completed, step.Status);
//    }

//    [Fact]
//    public void Build_WhenStepContainsNullableValues_BuildsNullableValues()
//    {
//        var registration =
//            CreateRegistration<NullableValueStep, NullableValueStepResult, NullableValueStepHandler>(
//                "nullable-value-step");

//        var registry =
//            CreateRegistry(
//                "nullable-value-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var dueDate =
//            new DateTimeOffset(
//                2026,
//                8,
//                3,
//                12,
//                30,
//                0,
//                TimeSpan.Zero);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["nullable-value-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Count"] = "7",
//                            ["DueDate"] = dueDate
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<NullableValueStep>(candidate.Step);

//        Assert.Equal(7, step.Count);
//        Assert.Equal(dueDate, step.DueDate);
//    }

//    [Fact]
//    public void Build_WhenStepContainsComplexObject_BuildsComplexObject()
//    {
//        var registration =
//            CreateRegistration<ComplexObjectStep, ComplexObjectStepResult, ComplexObjectStepHandler>(
//                "complex-object-step");

//        var registry =
//            CreateRegistry(
//                "complex-object-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["complex-object-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Customer"] =
//                                new Dictionary<string, object?>
//                                {
//                                    ["Id"] = "CUST-001",
//                                    ["Name"] = "Andrew",
//                                    ["Address"] =
//                                        new Dictionary<string, object?>
//                                        {
//                                            ["Street"] = "123 Main",
//                                            ["City"] = "Crown Point",
//                                            ["State"] = "IN"
//                                        }
//                                }
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<ComplexObjectStep>(candidate.Step);

//        Assert.NotNull(step.Customer);
//        Assert.Equal("CUST-001", step.Customer.Id);
//        Assert.Equal("Andrew", step.Customer.Name);

//        Assert.NotNull(step.Customer.Address);
//        Assert.Equal("123 Main", step.Customer.Address.Street);
//        Assert.Equal("Crown Point", step.Customer.Address.City);
//        Assert.Equal("IN", step.Customer.Address.State);
//    }

//    [Fact]
//    public void Build_WhenStepContainsCollectionOfPrimitives_BuildsCollection()
//    {
//        var registration =
//            CreateRegistration<PrimitiveCollectionStep, PrimitiveCollectionStepResult, PrimitiveCollectionStepHandler>(
//                "primitive-collection-step");

//        var registry =
//            CreateRegistry(
//                "primitive-collection-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["primitive-collection-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Tags"] =
//                                new[]
//                                {
//                                    "alpha",
//                                    "beta",
//                                    "gamma"
//                                }
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<PrimitiveCollectionStep>(candidate.Step);

//        Assert.Equal(3, step.Tags.Count);
//        Assert.Contains("alpha", step.Tags);
//        Assert.Contains("beta", step.Tags);
//        Assert.Contains("gamma", step.Tags);
//    }

//    [Fact]
//    public void Build_WhenStepContainsCollectionOfComplexObjects_BuildsCollection()
//    {
//        var registration =
//            CreateRegistration<ComplexCollectionStep, ComplexCollectionStepResult, ComplexCollectionStepHandler>(
//                "complex-collection-step");

//        var registry =
//            CreateRegistry(
//                "complex-collection-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["complex-collection-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Lines"] =
//                                new[]
//                                {
//                                    new Dictionary<string, object?>
//                                    {
//                                        ["Sku"] = "ABC",
//                                        ["Quantity"] = "2",
//                                        ["Price"] = "10.50"
//                                    },
//                                    new Dictionary<string, object?>
//                                    {
//                                        ["Sku"] = "XYZ",
//                                        ["Quantity"] = 3,
//                                        ["Price"] = 20.25m
//                                    }
//                                }
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<ComplexCollectionStep>(candidate.Step);

//        Assert.Equal(2, step.Lines.Count);

//        var first =
//            step.Lines.ElementAt(0);

//        Assert.Equal("ABC", first.Sku);
//        Assert.Equal(2, first.Quantity);
//        Assert.Equal(10.50m, first.Price);

//        var second =
//            step.Lines.ElementAt(1);

//        Assert.Equal("XYZ", second.Sku);
//        Assert.Equal(3, second.Quantity);
//        Assert.Equal(20.25m, second.Price);
//    }

//    [Fact]
//    public void Build_WhenStepContainsNestedRecordCollection_BuildsNestedRecordCollection()
//    {
//        var registration =
//            CreateRegistration<RecordCollectionStep, RecordCollectionStepResult, RecordCollectionStepHandler>(
//                "record-collection-step");

//        var registry =
//            CreateRegistry(
//                "record-collection-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["record-collection-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["OrderId"] = "ORD-001",
//                            ["Lines"] =
//                                new[]
//                                {
//                                    new Dictionary<string, object?>
//                                    {
//                                        ["Sku"] = "ABC",
//                                        ["Quantity"] = "2"
//                                    },
//                                    new Dictionary<string, object?>
//                                    {
//                                        ["Sku"] = "XYZ",
//                                        ["Quantity"] = 3
//                                    }
//                                }
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<RecordCollectionStep>(candidate.Step);

//        Assert.Equal("ORD-001", step.OrderId);
//        Assert.Equal(2, step.Lines.Count);

//        Assert.Equal("ABC", step.Lines.ElementAt(0).Sku);
//        Assert.Equal(2, step.Lines.ElementAt(0).Quantity);

//        Assert.Equal("XYZ", step.Lines.ElementAt(1).Sku);
//        Assert.Equal(3, step.Lines.ElementAt(1).Quantity);
//    }

//    [Fact]
//    public void Build_WhenNumericValuesAreStrings_BuildsStep()
//    {
//        var registration =
//            CreateRegistration<NumericStringStep, NumericStringStepResult, NumericStringStepHandler>(
//                "numeric-string-step");

//        var registry =
//            CreateRegistry(
//                "numeric-string-step",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["numeric-string-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["IntValue"] = "10",
//                            ["DecimalValue"] = "25.75",
//                            ["DoubleValue"] = "123.456"
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<NumericStringStep>(candidate.Step);

//        Assert.Equal(10, step.IntValue);
//        Assert.Equal(25.75m, step.DecimalValue);
//        Assert.Equal(123.456d, step.DoubleValue);
//    }

//    [Fact]
//    public void Build_WhenUnknownPropertyIsProvided_IgnoresUnknownPropertyForV1()
//    {
//        var registration =
//            CreateRegistration<SimpleClassStep, SimpleClassStepResult, SimpleClassStepHandler>(
//                "simple-class");

//        var registry =
//            CreateRegistry(
//                "simple-class",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["simple-class"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Name"] = "Andrew",
//                            ["Quantity"] = 42,
//                            ["IsActive"] = true,
//                            ["DoesNotExist"] = "ignored-for-v1"
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Equal(StepCandidateStatus.Built, candidate.Status);
//        Assert.False(candidate.HasErrors);

//        var step =
//            Assert.IsType<SimpleClassStep>(candidate.Step);

//        Assert.Equal("Andrew", step.Name);
//        Assert.Equal(42, step.Quantity);
//        Assert.True(step.IsActive);
//    }

//    [Fact]
//    public void Build_WhenConversionFails_ReturnsInvalidCandidate()
//    {
//        var registration =
//            CreateRegistration<SimpleClassStep, SimpleClassStepResult, SimpleClassStepHandler>(
//                "simple-class");

//        var registry =
//            CreateRegistry(
//                "simple-class",
//                registration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["simple-class"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Name"] = "Andrew",
//                            ["Quantity"] = "this-is-not-an-int",
//                            ["IsActive"] = true
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        var candidate =
//            Assert.Single(candidates);

//        Assert.Null(candidate.Step);
//        Assert.True(candidate.HasErrors);

//        Assert.Contains(
//            candidate.Messages,
//            x => x.Code == StepProcessingMessageCode.InvalidRequest);
//    }

//    [Fact]
//    public void Build_WhenMultipleStepsAreRequested_ReturnsCandidateForEachStep()
//    {
//        var simpleRegistration =
//            CreateRegistration<SimpleClassStep, SimpleClassStepResult, SimpleClassStepHandler>(
//                "simple-class");

//        var recordRegistration =
//            CreateRegistration<RecordStep, RecordStepResult, RecordStepHandler>(
//                "record-step");

//        var registry =
//            new Mock<IProcessStepRegistry>(MockBehavior.Strict);

//        registry
//            .Setup(x => x.Find("simple-class"))
//            .Returns(simpleRegistration);

//        registry
//            .Setup(x => x.Find("record-step"))
//            .Returns(recordRegistration);

//        var builder =
//            new StepCandidateBuilder(
//                registry.Object);

//        var customerId =
//            Guid.NewGuid();

//        var request =
//            CreateRequest(
//                new Dictionary<string, IReadOnlyDictionary<string, object?>>
//                {
//                    ["simple-class"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["Name"] = "Andrew",
//                            ["Quantity"] = 1,
//                            ["IsActive"] = true
//                        },
//                    ["record-step"] =
//                        new Dictionary<string, object?>
//                        {
//                            ["CustomerId"] = customerId,
//                            ["Name"] = "Ross"
//                        }
//                });

//        var candidates =
//            builder.Build(request);

//        Assert.Equal(2, candidates.Count);

//        Assert.Contains(
//            candidates,
//            x =>
//                x.StepName == "simple-class" &&
//                x.Step is SimpleClassStep &&
//                x.Status == StepCandidateStatus.Built);

//        Assert.Contains(
//            candidates,
//            x =>
//                x.StepName == "record-step" &&
//                x.Step is RecordStep &&
//                x.Status == StepCandidateStatus.Built);
//    }

//    private static Mock<IProcessStepRegistry> CreateRegistry(
//        string stepName,
//        ProcessStepRegistration registration)
//    {
//        var registry =
//            new Mock<IProcessStepRegistry>(MockBehavior.Strict);

//        registry
//            .Setup(x => x.Find(stepName))
//            .Returns(registration);

//        return registry;
//    }

//    private static ProcessStepRegistration CreateRegistration<TStep, TResult, THandler>(
//        string name)
//    {
//        return new ProcessStepRegistration(
//            typeof(TStep),
//            typeof(TResult),
//            typeof(THandler),
//             [],
//            [],
//            [],
//            new RepeatableOptions(),
//           new ProcessStepMetadata(
//                name,
//                $"{name} description.",
//                "1.0",
//                $"{name} displayname."));
//    }

//    private static ProcessorRequest CreateRequest(
//        IReadOnlyDictionary<string, object> steps)
//    {
//        return new ProcessorRequest
//        {
//            Steps = steps
//        };
//    }

//    private abstract class TestHandler<TProcessStep, TProcessStepResult> :
//        IProcessStepHandler<TProcessStep, TProcessStepResult>
//    {
//        public Task<ProcessStepHandlerResult<TProcessStepResult>> ExecuteAsync(
//            TProcessStep processStep,
//            ProcessStepContext context,
//            CancellationToken cancellationToken = default)
//        {
//            throw new InvalidOperationException(
//                "StepCandidateBuilder tests should never execute handlers.");
//        }
//    }

//    [ProcessStep(Name = "simple-class", Description = "Simple class step.", Version = "1.0")]
//    private sealed class SimpleClassStep
//    {
//        public string Name { get; set; } = string.Empty;

//        public int Quantity { get; set; }

//        public bool IsActive { get; set; }
//    }

//    private sealed class SimpleClassStepResult;

//    private sealed class SimpleClassStepHandler :
//        TestHandler<SimpleClassStep, SimpleClassStepResult>;

//    [ProcessStep(Name = "init-property", Description = "Init property step.", Version = "1.0")]
//    private sealed class InitPropertyStep
//    {
//        public string RequestId { get; init; } = string.Empty;

//        public decimal Amount { get; init; }
//    }

//    private sealed class InitPropertyStepResult;

//    private sealed class InitPropertyStepHandler :
//        TestHandler<InitPropertyStep, InitPropertyStepResult>;

//    [ProcessStep(Name = "record-step", Description = "Record step.", Version = "1.0")]
//    private sealed record RecordStep(
//        Guid CustomerId,
//        string Name);

//    private sealed class RecordStepResult;

//    private sealed class RecordStepHandler :
//        TestHandler<RecordStep, RecordStepResult>;

//    [ProcessStep(Name = "enum-step", Description = "Enum step.", Version = "1.0")]
//    private sealed class EnumStep
//    {
//        public TestStepStatus Status { get; init; }
//    }

//    private sealed class EnumStepResult;

//    private sealed class EnumStepHandler :
//        TestHandler<EnumStep, EnumStepResult>;

//    [ProcessStep(Name = "nullable-enum-step", Description = "Nullable enum step.", Version = "1.0")]
//    private sealed class NullableEnumStep
//    {
//        public TestStepStatus? Status { get; init; }
//    }

//    private sealed class NullableEnumStepResult;

//    private sealed class NullableEnumStepHandler :
//        TestHandler<NullableEnumStep, NullableEnumStepResult>;

//    [ProcessStep(Name = "nullable-value-step", Description = "Nullable value step.", Version = "1.0")]
//    private sealed class NullableValueStep
//    {
//        public int? Count { get; init; }

//        public DateTimeOffset? DueDate { get; init; }
//    }

//    private sealed class NullableValueStepResult;

//    private sealed class NullableValueStepHandler :
//        TestHandler<NullableValueStep, NullableValueStepResult>;

//    [ProcessStep(Name = "complex-object-step", Description = "Complex object step.", Version = "1.0")]
//    private sealed class ComplexObjectStep
//    {
//        public Customer? Customer { get; init; }
//    }

//    private sealed class ComplexObjectStepResult;

//    private sealed class ComplexObjectStepHandler :
//        TestHandler<ComplexObjectStep, ComplexObjectStepResult>;

//    private sealed class Customer
//    {
//        public string Id { get; init; } = string.Empty;

//        public string Name { get; init; } = string.Empty;

//        public Address? Address { get; init; }
//    }

//    private sealed class Address
//    {
//        public string Street { get; init; } = string.Empty;

//        public string City { get; init; } = string.Empty;

//        public string State { get; init; } = string.Empty;
//    }

//    [ProcessStep(Name = "primitive-collection-step", Description = "Primitive collection step.", Version = "1.0")]
//    private sealed class PrimitiveCollectionStep
//    {
//        public IReadOnlyCollection<string> Tags { get; init; } =
//            [];
//    }

//    private sealed class PrimitiveCollectionStepResult;

//    private sealed class PrimitiveCollectionStepHandler :
//        TestHandler<PrimitiveCollectionStep, PrimitiveCollectionStepResult>;

//    [ProcessStep(Name = "complex-collection-step", Description = "Complex collection step.", Version = "1.0")]
//    private sealed class ComplexCollectionStep
//    {
//        public IReadOnlyCollection<OrderLine> Lines { get; init; } =
//            [];
//    }

//    private sealed class ComplexCollectionStepResult;

//    private sealed class ComplexCollectionStepHandler :
//        TestHandler<ComplexCollectionStep, ComplexCollectionStepResult>;

//    private sealed class OrderLine
//    {
//        public string Sku { get; init; } = string.Empty;

//        public int Quantity { get; init; }

//        public decimal Price { get; init; }
//    }

//    [ProcessStep(Name = "record-collection-step", Description = "Record collection step.", Version = "1.0")]
//    private sealed record RecordCollectionStep(
//        string OrderId,
//        IReadOnlyCollection<RecordOrderLine> Lines);

//    private sealed record RecordOrderLine(
//        string Sku,
//        int Quantity);

//    private sealed class RecordCollectionStepResult;

//    private sealed class RecordCollectionStepHandler :
//        TestHandler<RecordCollectionStep, RecordCollectionStepResult>;

//    [ProcessStep(Name = "numeric-string-step", Description = "Numeric string step.", Version = "1.0")]
//    private sealed class NumericStringStep
//    {
//        public int IntValue { get; init; }

//        public decimal DecimalValue { get; init; }

//        public double DoubleValue { get; init; }
//    }

//    private sealed class NumericStringStepResult;

//    private sealed class NumericStringStepHandler :
//        TestHandler<NumericStringStep, NumericStringStepResult>;

//    private enum TestStepStatus
//    {
//        Unknown = 0,

//        WaitingForApproval = 1,

//        Completed = 2
//    }
//}