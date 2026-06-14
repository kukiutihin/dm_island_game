namespace DMIslandClient.Audio

open OpenTK.Audio.OpenAL

module Audio =
    let device =
        let device = ALC.OpenDevice(null)
        let context = ALC.CreateContext(device, Array.zeroCreate 1)
        ALC.MakeContextCurrent(context)
