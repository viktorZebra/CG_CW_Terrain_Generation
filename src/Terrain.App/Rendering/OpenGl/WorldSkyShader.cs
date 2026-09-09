namespace Terrain.App.Rendering.OpenGl;

// Shared by background and distance fog; paired with Terrain.Core.World.DayNight.Sky.
internal static class WorldSkyShader
{
    public const string Functions = """
        float worldSunAngle(float seconds) {
            float time = mod(seconds,480.0);
            return (time < 300.0 ? time/300.0 : 1.0+(time-300.0)/180.0)*3.14159265359;
        }
        vec3 worldSky(vec3 ray, float seconds, float pixel) {
            float angle = worldSunAngle(seconds);
            vec3 source = vec3(-cos(angle), sin(angle), 0.0);
            float day = smoothstep(-.12, .18, source.y);
            vec3 top = mix(vec3(.003,.006,.025), vec3(.07,.19,.34),day);
            vec3 horizon = mix(vec3(.018,.027,.065), mix(vec3(.72,.30,.17),vec3(.49,.67,.76),max(0.0,source.y)),day);
            vec3 sky = mix(horizon,top,clamp(ray.y,0.0,1.0));
            vec3 p = ray * 180.0;
            ivec3 cell = ivec3(floor(p));
            uint hash = uint(cell.x)*73856093u ^ uint(cell.y)*19349663u ^ uint(cell.z)*83492791u;
            hash ^= hash >> 13; hash *= 1274126177u; hash ^= hash >> 16;
            if (hash % 1000u < 9u && ray.y > .04)
                sky += vec3(.7,.8,1.0)*(1.0-smoothstep(.12,.48,length(p-vec3(cell)-.5)))*(1.0-day);
            float sd = length(ray-source), md = length(ray+source);
            float sunDisk = (1.0-smoothstep(.018,.018+pixel*2.0,sd))*smoothstep(-.03,.01,source.y);
            float moonDisk = (1.0-smoothstep(.024,.024+pixel*2.0,md))*(1.0-day);
            sky += vec3(1.0,.6,.22)*(.18*exp(-sd*sd/.014))*day;
            sky = mix(sky,vec3(1.0,.90,.65),sunDisk);
            if (moonDisk <= 0.0) return clamp(sky,0.0,1.0);
            vec3 moon = -source;
            vec2 uv = vec2(dot(ray,vec3(moon.y,-moon.x,0.0)),ray.z)/.024;
            vec3 craters[4] = vec3[4](vec3(-.3,.2,.28),vec3(.25,-.28,.32),vec3(.37,.38,.16),vec3(-.4,-.4,.16));
            float crater = 0.0;
            for (int i=0;i<4;i++) crater += 1.0-smoothstep(craters[i].z*.65,craters[i].z,length(uv-craters[i].xy));
            float sphere = sqrt(max(0.0,1.0-dot(uv,uv)));
            vec3 moonColor = vec3(.72,.79,.86)*(.72+.28*sphere-.12*crater);
            return clamp(mix(sky,moonColor,moonDisk),0.0,1.0);
        }
        float cloudHash(ivec2 cell, uint seed) {
            uint h = uint(cell.x)*73856093u ^ uint(cell.y)*19349663u ^ seed;
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            return float(h & 0xffffffu) / 16777215.0;
        }
        float cloudNoise(vec2 p, uint seed) {
            ivec2 cell = ivec2(floor(p));
            vec2 f = smoothstep(vec2(0.0),vec2(1.0),p-vec2(cell));
            float a = mix(cloudHash(cell,seed),cloudHash(cell+ivec2(1,0),seed),f.x);
            float b = mix(cloudHash(cell+ivec2(0,1),seed),cloudHash(cell+ivec2(1,1),seed),f.x);
            return mix(a,b,f.y);
        }
        vec3 cloudyWorldSky(vec3 ray, vec3 camera, float seconds, float pixel, vec3 cloudState) {
            vec3 sky = worldSky(ray,seconds,pixel);
            if (cloudState.x < 0.0 || ray.y <= .025 || camera.y >= 12.0) return sky;
            uint seed = uint(cloudState.x) | (uint(cloudState.y) << 16);
            vec3 position = camera + ray*((12.0-camera.y)/ray.y);
            vec2 p = (position.xz-vec2(.10,.035)*cloudState.z)*.13;
            float density = cloudNoise(p,seed)*.57 + cloudNoise(p*2.03+vec2(13.2,-7.1),seed)*.27
                + cloudNoise(p*4.11+vec2(-3.4,19.7),seed)*.11 + cloudNoise(p*8.21+vec2(27.1,5.3),seed)*.05;
            float opacity = smoothstep(.48,.70,density)*.78*smoothstep(.025,.16,ray.y);
            float sunHeight = sin(worldSunAngle(seconds));
            float day = smoothstep(-.12,.18,sunHeight);
            vec3 daylightColor = mix(vec3(.92,.58,.39),vec3(.93,.95,.98),smoothstep(0.0,.4,clamp(sunHeight,0.0,1.0)));
            vec3 color = mix(vec3(.085,.11,.17),daylightColor,day)*(.82+opacity*.18);
            return mix(sky,color,opacity);
        }
        """;
}
