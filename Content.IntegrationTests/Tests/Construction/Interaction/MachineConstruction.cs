using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._DV.Construction; // HS
using Content.Shared.Lathe; // HS

namespace Content.IntegrationTests.Tests.Construction.Interaction;

public sealed class MachineConstruction : InteractionTest
{
    private const string MachineFrame = "MachineFrame";
    private const string Unfinished = "UnfinishedMachineFrame";
    private const string ProtolatheBoard = "ProtolatheMachineCircuitboard";
    private const string Protolathe = "Protolathe";
    private const string Beaker = "Beaker";

    [Test]
    public async Task ConstructProtolathe()
    {
        await StartConstruction(MachineFrame);
        await InteractUsing(Steel, 5);
        ClientAssertPrototype(Unfinished, Target);
        await Interact(Wrench, Cable);
        AssertPrototype(MachineFrame);
        await Interact(ProtolatheBoard, Manipulator1, Manipulator1, Manipulator1, Manipulator1, Beaker, Beaker, Screw);
        AssertPrototype(Protolathe);
    }

    [Test]
    public async Task DeconstructProtolathe()
    {
        await StartDeconstruction(Protolathe);
        await Interact(Screw, Pry);
        AssertPrototype(MachineFrame);
        await Interact(Pry, Cut);
        AssertPrototype(Unfinished);
        await Interact(Wrench, Screw);
        AssertDeleted();
        await AssertEntityLookup(
            (Steel, 5),
            (Cable, 1),
            (Beaker, 2),
            (Manipulator1, 4),
            (ProtolatheBoard, 1));
    }

    [Test]
    public async Task ChangeMachine()
    {
        // Partially deconstruct a protolathe.
        await SpawnTarget(Protolathe);
        await Interact(Screw, Pry, Pry);
        AssertPrototype(MachineFrame);

        // Change it into an autolathe
        await InteractUsing("AutolatheMachineCircuitboard");
        AssertPrototype(MachineFrame);
        await Interact(Manipulator1, Manipulator1, Manipulator1, Manipulator1, Glass, Screw);
        AssertPrototype("Autolathe");
    }

    // HS upgraded lathe test
    [Test]
    public async Task UpgradeLathe()
    {
        await SpawnTarget(Protolathe);

        float timeBefore = 0;
        float materialBefore = 0;
        await Server.WaitAssertion(() =>
        {
            var lathe = SEntMan.GetComponent<LatheComponent>(SEntMan.GetEntity(Target!.Value));
            timeBefore = lathe.FinalTimeMultiplier;
            materialBefore = lathe.FinalMaterialUseMultiplier;
        });

        await Interact(Screw, "LatheUpgradeKitHyper");

        await Server.WaitAssertion(() =>
        {
            var target = SEntMan.GetEntity(Target!.Value);
            var lathe = SEntMan.GetComponent<LatheComponent>(target);
            Assert.That(SEntMan.HasComponent<UpgradedMachineComponent>(target), Is.True);
            Assert.That(lathe.FinalTimeMultiplier, Is.EqualTo(timeBefore * 1.5f).Within(0.001f));
            Assert.That(lathe.FinalMaterialUseMultiplier, Is.EqualTo(materialBefore * 0.5f).Within(0.001f));
        });
    }
}

