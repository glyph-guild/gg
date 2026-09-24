namespace Gg.Contracts.Tests;

/// <summary>
/// A slot's hostname and its credential are spelled by one method, here.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two derivations that agree today is how a runner ends up hunting for a
/// file the CLI never wrote.</b> That sentence is already in this repository,
/// over <c>CredentialLocator</c>, and it was written about exactly this: three
/// places touch a slot's names - the control plane grants one, gg registers the
/// credential under it, the runner dials with it - and each of them had its own
/// string replace.
/// </para>
/// <para>
/// <b>The pattern is the tenant's and is never re-invented.</b> It comes off the
/// exposure document, which the tenant may edit; nothing may guess at what it
/// looks like. All this method does is put the slot where the document said to.
/// </para>
/// </remarks>
public class ASlotIsSpelledOnceTests
{
    [Test]
    public async Task A_slots_hostname_is_the_pattern_with_its_ordinal_in_place()
    {
        await Assert.That(Exposure.Spelled("jdapp-{slot}.goodgrief.dev", 1))
            .IsEqualTo("jdapp-01.goodgrief.dev");
    }

    [Test]
    public async Task Two_digits_so_a_ninth_slot_does_not_reorder_the_first_eight()
    {
        // The ordering a PERSON reads, in a console listing eight addresses.
        // "jdapp-9" sorts before "jdapp-10" nowhere a human would expect.
        await Assert.That(Exposure.Spelled("{slot}", 8)).IsEqualTo("08");
        await Assert.That(Exposure.Spelled("{slot}", 12)).IsEqualTo("12");
    }

    [Test]
    public async Task The_credential_pattern_is_spelled_by_the_same_method()
    {
        // ONE METHOD FOR BOTH, which is the point. The control plane grants a
        // hostname and a locator in the same breath, and gg writes the secret
        // under that locator on another machine at another time. A second
        // implementation of either is a secret filed where nothing looks.
        await Assert.That(Exposure.Spelled("local:exposure/jdapp-{slot}", 3))
            .IsEqualTo("local:exposure/jdapp-03");
    }

    [Test]
    public async Task Every_occurrence_is_replaced_rather_than_the_first()
    {
        // A pattern naming the slot twice is odd but legal, and a first-only
        // replace would produce a name that is half one slot and half a
        // literal - which resolves, and answers, and is nobody's address.
        await Assert.That(Exposure.Spelled("{slot}-of-{slot}", 4)).IsEqualTo("04-of-04");
    }

    [Test]
    public async Task A_pattern_with_no_slot_in_it_is_refused_rather_than_returned()
    {
        // THE REPLICA COLLISION, AND IT IS SILENT WHEN IT HAPPENS. One name for
        // every slot means two flights dial one tunnel; the provider takes the
        // second connector as a replica and serves each request from whichever
        // is nearer, with no error anywhere. Validate refuses such a document at
        // apply, so reaching here without a token is impossible - and a method
        // that quietly returned the pattern would make "impossible" the only
        // thing standing between two flights and each other's screens.
        await Assert.That(() => Exposure.Spelled("jdapp.goodgrief.dev", 1))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task A_slot_below_one_is_refused()
    {
        // Slots are one-based everywhere a person sees them, and zero is what
        // an off-by-one produces. "00" is a name the inventory never contains.
        await Assert.That(() => Exposure.Spelled("jdapp-{slot}", 0))
            .Throws<ArgumentOutOfRangeException>();
    }
}
