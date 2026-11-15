using Nucleus.Serializers;

// Bundle id for the Kinematic Character Controller integration's networked types. It sits in the reserved range below the framework bundle, under the Unity integration (one below),
// its tests (two and three below) and the demos (four below), so it can never take bundle zero, which every game assembly without
// its own [NetworkBundle] uses.
[assembly: NetworkBundle((ushort)(NetworkTypeRegistry.FrameworkBundleId - 5))]
