namespace LadaEngine

open System.Collections.Generic
open System.IO
open OpenTK.Graphics.OpenGL4
open OpenTK.Mathematics

/// A linked OpenGL shader program with its uniform locations
type Shader =
    { Handle: int
      Uniforms: IReadOnlyDictionary<string, int> }

module Shader =
    let private compile (shaderType: ShaderType) (source: string) =
        let handle = GL.CreateShader shaderType
        GL.ShaderSource(handle, source)
        GL.CompileShader handle
        let mutable status = 0
        GL.GetShader(handle, ShaderParameter.CompileStatus, &status)
        if status <> int All.True then
            let infoLog = GL.GetShaderInfoLog handle
            Misc.printShaderError source infoLog
            failwith $"Error occurred whilst compiling Shader({handle}).\n\n{infoLog}"
        handle

    let private link (program: int) =
        GL.LinkProgram program
        let mutable status = 0
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, &status)
        if status <> int All.True then
            failwith $"Error occurred whilst linking Program({program})"

    let private readUniforms (handle: int) : IReadOnlyDictionary<string, int> =
        let mutable count = 0
        GL.GetProgram(handle, GetProgramParameterName.ActiveUniforms, &count)
        let uniforms = Dictionary<string, int>()
        for i in 0 .. count - 1 do
            let mutable size = 0
            let mutable uniformType = ActiveUniformType.Bool
            let name = GL.GetActiveUniform(handle, i, &size, &uniformType)
            uniforms[name] <- GL.GetUniformLocation(handle, name)
        uniforms :> IReadOnlyDictionary<string, int>

    /// Wraps an already linked GL program handle
    let ofProgram (programHandle: int) : Shader =
        { Handle = programHandle; Uniforms = readUniforms programHandle }

    /// Compiles and links a shader from vertex and fragment source code
    let ofSource (vertexSource: string) (fragmentSource: string) : Shader =
        let vertex = compile ShaderType.VertexShader vertexSource
        let fragment = compile ShaderType.FragmentShader fragmentSource

        let handle = GL.CreateProgram()
        GL.AttachShader(handle, vertex)
        GL.AttachShader(handle, fragment)
        link handle
        GL.DetachShader(handle, vertex)
        GL.DetachShader(handle, fragment)
        GL.DeleteShader fragment
        GL.DeleteShader vertex

        { Handle = handle; Uniforms = readUniforms handle }

    /// Compiles and links a shader from vertex and fragment source files
    let ofFiles (vertexPath: string) (fragmentPath: string) : Shader =
        ofSource (File.ReadAllText vertexPath) (File.ReadAllText fragmentPath)

    /// Makes the shader program current (skipped if already current)
    let activate (shader: Shader) =
        if shader.Handle <> GlState.lastShaderUsed then
            GL.UseProgram shader.Handle
            GlState.lastShaderUsed <- shader.Handle

    /// Location of a vertex attribute
    let attribLocation (name: string) (shader: Shader) =
        GL.GetAttribLocation(shader.Handle, name)

    /// Location of a uniform, or -1 if it does not exist
    let uniformLocation (name: string) (shader: Shader) =
        match shader.Uniforms.TryGetValue name with
        | true, location -> location
        | false, _ -> -1

    let private withLocation (name: string) (shader: Shader) (action: int -> unit) =
        match shader.Uniforms.TryGetValue name with
        | true, location ->
            activate shader
            action location
        | false, _ -> ()

    let setInt (name: string) (data: int) (shader: Shader) =
        withLocation name shader (fun loc -> GL.Uniform1(loc, data))

    let setFloat (name: string) (data: float32) (shader: Shader) =
        withLocation name shader (fun loc -> GL.Uniform1(loc, data))

    let setVector2 (name: string) (data: Vector2) (shader: Shader) =
        withLocation name shader (fun loc -> GL.Uniform2(loc, data))

    let setVector3 (name: string) (data: Vector3) (shader: Shader) =
        withLocation name shader (fun loc -> GL.Uniform3(loc, data))

    let setVector4 (name: string) (data: Vector4) (shader: Shader) =
        withLocation name shader (fun loc -> GL.Uniform4(loc, data))

    /// Sets a Matrix4 uniform (transposed before sending, like the original engine)
    let setMatrix4 (name: string) (data: Matrix4) (shader: Shader) =
        match shader.Uniforms.TryGetValue name with
        | true, location ->
            activate shader
            let mutable m = data
            GL.UniformMatrix4(location, true, &m)
        | false, _ -> ()

    /// Sets an array of ints at a known uniform location
    let setIntGroup (location: int) (count: int) (data: int[]) (shader: Shader) =
        activate shader
        GL.Uniform1(location, count, data)

    /// Sets an array of Vector4 (as flat floats) at a known uniform location
    let setVector4Group (location: int) (count: int) (data: float32[]) (shader: Shader) =
        activate shader
        GL.Uniform4(location, count, data)

/// Standard shader sources
module Shaders =
    /// Plain fullscreen vertex shader (no camera support)
    let standardVert =
        """#version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec2 aTexCoord;
        out vec2 texCoord;

        void main(void)
        {
            texCoord = aTexCoord;
            gl_Position = vec4(aPosition, 1.0);
        }"""

    /// Plain textured fragment shader
    let standardFrag =
        """#version 330
        out vec4 outputColor;
        in vec2 texCoord;
        uniform sampler2D texture0;
        void main()
        {
            outputColor = texture(texture0, texCoord);
        }"""

    /// Vertex shader with camera position and zoom uniforms
    let cameraVert =
        """#version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec2 aTexCoord;

        uniform vec2 position;
        uniform float zoom;

        out vec2 texCoord;

        void main(void)
        {
            texCoord = aTexCoord;
            vec2 p = (aPosition.xy - position.xy) / zoom;
            gl_Position = vec4(p, aPosition.z + 0.1, 1.0);
        }"""
