namespace DMIslandClient.World

module RoomRendererShaders =
    let waterFrag =
        """#version 330
        out vec4 outputColor;
        in vec2 texCoord;
        in vec2 fragCoord;
        uniform sampler2D texture0;
        uniform float time;

        float timeSpeed = 2.0;

        float randomVal (float inVal)
        {
            return fract(sin(dot(vec2(inVal, 2523.2361) ,vec2(12.9898,78.233))) * 43758.5453)-0.5;
        }

        vec2 randomVec2 (float inVal)
        {
            return normalize(vec2(randomVal(inVal), randomVal(inVal+151.523)));
        }

        float makeWaves(vec2 uv, float theTime, float offset)
        {
            float result = 0.0;
            float direction = 0.0;
            float sineWave = 0.0;
            vec2 randVec = vec2(1.0,0.0);
            float i;
            for(int n = 0; n < 5; n++)
            {
                i = float(n)+offset;
                randVec = randomVec2(float(i));
  		        direction = (uv.x*randVec.x+uv.y*randVec.y);
                sineWave = sin(direction*randomVal(i+1.6516)+theTime*timeSpeed);
                sineWave = smoothstep(0.0,1.0,sineWave);
    	        result += randomVal(i+123.0)*sineWave;
            }
            return result;
        }

        void main()
        {
            vec2 uv = fragCoord;
            vec2 uv2 = uv * 20.0;
            uv *= 2.0;
            
            float result = 0.0;
            float result2 = 0.0;
            
            result = makeWaves( uv2+vec2(time * timeSpeed,0.0), time, 0.1);
            result2 = makeWaves( uv2-vec2(time * 0.8 * timeSpeed,0.0), time*0.8+0.06, 0.26);
            result = smoothstep(0.4,1.1,1.0-abs(result));
            result2 = smoothstep(0.4,1.1,1.0-abs(result2));
            result = 2.0*smoothstep(0.35,1.8,(result+result2)*0.5);
            
            vec2 p = vec2(result, result2)*.015 + sin(uv*16. - cos(uv.yx*16. + time*timeSpeed))*.015; // Etc.
	        outputColor = vec4(result)*0.1+texture(texture0, texCoord + p);
        }"""

    let waterVert =
        """#version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec2 aTexCoord;

        uniform vec2 position;
        uniform float zoom;

        out vec2 texCoord;
        out vec2 fragCoord;

        void main(void)
        {
            texCoord = aTexCoord;
            fragCoord = aPosition.xy;
            vec2 p = (aPosition.xy - position.xy) / zoom;
            gl_Position = vec4(p, aPosition.z + 0.1, 1.0);
        }"""