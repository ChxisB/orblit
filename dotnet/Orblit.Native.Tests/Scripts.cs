namespace Orblit.Native.Tests;

/// <summary>The C++ the tests compile. Real scripts, so a test sees them run rather than assume they would.</summary>
internal static class Scripts
{
    /// <summary>
    /// A script that counts into a component, so a test can see that it ran by
    /// looking at the world rather than at what it printed. Two doubles rather
    /// than a double and an int, so the column reads back as one typed view: the
    /// component layout is the contract between the two sides here just as much
    /// as the function table is.
    /// </summary>
    public const string Counter = """
        #include "orblit_script.h"

        struct Ticks { double seconds; double calls; };

        namespace {
        OrblitComponent tick;
        OrblitEntity subject;
        }

        ORBLIT_SCRIPT {
          tick = orblit::component<Ticks>("Ticks");
          subject = orblit::spawn();
          orblit::give(subject, tick, Ticks{0.0, 0.0});
          orblit::log("counter: started");
        }

        extern "C" void orblit_step(double delta) {
          Ticks *held = orblit::get<Ticks>(subject, tick);
          if (held == nullptr) return;
          held->seconds += delta;
          held->calls += 1.0;
        }

        extern "C" void orblit_stop(void) { orblit::log("counter: stopped"); }
        """;

    /// <summary>Reads a data object every frame, so values a designer edits reach C++.</summary>
    public const string Reader = """
        #include "orblit_script.h"

        struct Speed { double value; double on; };

        namespace {
        OrblitComponent speed;
        OrblitEntity subject;

        // Resolved once. Every read after this is a load from memory and crosses
        // nothing, which is what makes it safe to read inside a loop.
        const double *speedAt;
        const bool *bouncyAt;
        const char *const *labelAt;
        }

        ORBLIT_SCRIPT {
          speed = orblit::component<Speed>("Speed");
          subject = orblit::spawn();
          orblit::give(subject, speed, Speed{0.0, 0.0});
          speedAt = orblit::number_at("ball.odata", "speed", -1.0);
          bouncyAt = orblit::toggle_at("ball.odata", "bouncy", false);
          labelAt = orblit::text_at("ball.odata", "label");
        }

        extern "C" void orblit_step(double delta) {
          (void)delta;
          Speed *held = orblit::get<Speed>(subject, speed);
          held->value = *speedAt;
          held->on = *bouncyAt ? 1.0 : 0.0;
          if (*labelAt != nullptr) orblit::log(*labelAt);
        }

        extern "C" void orblit_stop(void) {}
        """;

    /// <summary>
    /// The same values read the slow way, for the test that checks the calls
    /// still work: a key that is not known until it is computed has no address
    /// to hold.
    /// </summary>
    public const string Caller = """
        #include "orblit_script.h"

        struct Speed { double value; double on; };

        namespace {
        OrblitComponent speed;
        OrblitEntity subject;
        }

        ORBLIT_SCRIPT {
          speed = orblit::component<Speed>("Speed");
          subject = orblit::spawn();
          orblit::give(subject, speed, Speed{0.0, 0.0});
        }

        extern "C" void orblit_step(double delta) {
          (void)delta;
          Speed *held = orblit::get<Speed>(subject, speed);
          held->value = orblit::number("ball.odata", "speed", -1.0);
          held->on = orblit::toggle("ball.odata", "bouncy", false) ? 1.0 : 0.0;
          const char *label = orblit::text("ball.odata", "label");
          if (label != nullptr) orblit::log(label);
        }

        extern "C" void orblit_stop(void) {}
        """;

    /// <summary>
    /// A script that reads one value a million times a frame, either through the
    /// address or by calling in for it. The difference between the two is the
    /// whole reason the addresses exist.
    /// </summary>
    public static string Loop(bool byAddress)
    {
        var read = byAddress ? "*speedAt" : "orblit::number(\"ball.odata\", \"speed\", 1.0)";
        return $$"""
            #include "orblit_script.h"

            struct Total { double sum; double calls; };

            namespace {
            OrblitComponent total;
            OrblitEntity subject;
            const double *speedAt;
            }

            ORBLIT_SCRIPT {
              total = orblit::component<Total>("Total");
              subject = orblit::spawn();
              orblit::give(subject, total, Total{0.0, 0.0});
              speedAt = orblit::number_at("ball.odata", "speed", 1.0);
            }

            extern "C" void orblit_step(double delta) {
              (void)delta;
              Total *held = orblit::get<Total>(subject, total);
              double sum = 0;
              for (int i = 0; i < 1000000; ++i) {
                sum += {{read}};
              }
              held->sum = sum;
              held->calls += 1.0;
            }

            extern "C" void orblit_stop(void) {}
            """;
    }

    /// <summary>A script that compiles and is one of ours, but answers a contract version nobody speaks.</summary>
    public const string Ancient = """
        #include "orblit_script.h"
        extern "C" uint32_t orblit_script_abi(void) { return 99; }
        extern "C" void orblit_start(const OrblitScriptHost *h) { (void)h; }
        extern "C" void orblit_step(double d) { (void)d; }
        extern "C" void orblit_stop(void) {}
        """;

    /// <summary>Asserts the host table's size in C++, because C++ is the side that has to agree.</summary>
    public static string Sizes(int bytes) => $$"""
        #include "orblit_script.h"
        static_assert(sizeof(OrblitScriptHost) == {{bytes}},
                      "the host table and the C# struct have drifted apart");
        ORBLIT_SCRIPT {}
        extern "C" void orblit_step(double d) { (void)d; }
        extern "C" void orblit_stop(void) {}
        """;
}
