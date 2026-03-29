(function () {
  /**
   * Loads a Wavefront .mtl file specifying materials
   */

  class MTLLoader extends THREE.Loader {
    constructor(manager) {
      super(manager);
    }
    /**
     * Loads and parses a MTL asset from a URL.
     *
     * @param {String} url - URL to the MTL file.
     * @param {Function} [onLoad] - Callback invoked with the loaded object.
     * @param {Function} [onProgress] - Callback for download progress.
     * @param {Function} [onError] - Callback for download errors.
     *
     * @see setPath setResourcePath
     *
     * @note In order for relative texture references to resolve correctly
     * you must call setResourcePath() explicitly prior to load.
     */

    getHashUrl(hash) {
      if (hash.includes('mats-thumbnails.roblox.com')) {
        return hash;
      }
      if (hash.includes('www.silrev.biz')) {
        return hash;
      }
      if (hash.includes('https://silrev.biz/')) {
        hash = hash.substring(str.indexOf('/', 8) + 1);
      }
      let st = 31;
      for (let ii = 0; ii < hash.length; ii++) {
        st ^= hash[ii].charCodeAt(0);
      }
      // return `https://t${(st % 8).toString()}.rbxcdn.com/${hash}`;
      return `https://www.silrev.biz${hash.at(0) === '/' ? hash : '/' + hash}`;
    }

    load(hash, onLoad, onProgress, onError) {
      const scope = this;
      const loader = new THREE.FileLoader(this.manager);
      const url = this.getHashUrl(hash);

      loader.setPath(this.path);
      loader.setRequestHeader(this.requestHeader);
      loader.setWithCredentials(this.withCredentials);
      loader.load(
        url,
        function (text) {
          try {
            onLoad(scope.parse(text));
          } catch (e) {
            if (onError) {
              onError(e);
            } else {
              console.error(e);
            }

            scope.manager.itemError(url);
          }
        },
        onProgress,
        onError
      );
    }

    setMaterialOptions(value) {
      this.materialOptions = value;
      return this;
    }
    /**
     * Parses a MTL file.
     *
     * @param {String} text - Content of MTL file
     * @return {MaterialCreator}
     *
     * @see setPath setResourcePath
     *
     * @note In order for relative texture references to resolve correctly
     * you must call setResourcePath() explicitly prior to parse.
     */

    parse(text) {
      const lines = text.split('\n');
      let info = {};
      const delimiter_pattern = /\s+/;
      const materialsInfo = {};

      for (let i = 0; i < lines.length; i++) {
        let line = lines[i];
        line = line.trim();

        if (line.length === 0 || line.charAt(0) === '#') {
          // Blank line or comment ignore
          continue;
        }

        const pos = line.indexOf(' ');
        let key = pos >= 0 ? line.substring(0, pos) : line;
        key = key.toLowerCase();
        let value = pos >= 0 ? line.substring(pos + 1) : '';
        value = value.trim();

        if (key === 'newmtl') {
          // New material
          info = {
            name: value
          };
          materialsInfo[value] = info;
        } else if (key === 'ka' || key === 'kd' || key === 'ks' || key === 'ke') {
          const ss = value.split(delimiter_pattern, 3);
          info[key] = [parseFloat(ss[0]), parseFloat(ss[1]), parseFloat(ss[2])];
        } else {
          info[key] = value;
        }
      }

      const materialCreator = new MaterialCreator(this.resourcePath || '', this.materialOptions);
      materialCreator.setCrossOrigin(this.crossOrigin);
      materialCreator.setManager(this.manager);
      materialCreator.setMaterials(materialsInfo);
      return materialCreator;
    }
  }
  /**
   * Create a new MTLLoader.MaterialCreator
   * @param baseUrl - Url relative to which textures are loaded
   * @param options - Set of options on how to construct the materials
   *                  side: Which side to apply the material
   *                        THREE.FrontSide (default), THREE.BackSide, THREE.DoubleSide
   *                  wrap: What type of wrapping to apply for textures
   *                        THREE.RepeatWrapping (default), THREE.ClampToEdgeWrapping, THREE.MirroredRepeatWrapping
   *                  normalizeRGB: RGBs need to be normalized to 0-1 from 0-255
   *                                Default: false, assumed to be already normalized
   *                  ignoreZeroRGBs: Ignore values of RGBs (Ka,Kd,Ks) that are all 0's
   *                                  Default: false
   * @constructor
   */

  class MaterialCreator {
    constructor(options = {}) {
      this.options = options;
      this.materialsInfo = {};
      this.materials = {};
      this.materialsArray = [];
      this.nameLookup = {};
      this.crossOrigin = 'anonymous';
      this.side = this.options.side !== undefined ? this.options.side : THREE.FrontSide;
      this.wrap = this.options.wrap !== undefined ? this.options.wrap : THREE.RepeatWrapping;

      // ROBLOX: Override map_fragment to blend diffuse map with base part color.
      THREE.ShaderChunk.map_fragment = `
		        #ifdef USE_MAP
		            vec4 sampledDiffuseColor = texture2D( map, vUv );
		            diffuseColor.rgb = mix(diffuseColor.rgb, sampledDiffuseColor.rgb, sampledDiffuseColor.a);
		        #endif`;

      // ROBLOX: Override roughnessmap_fragment to source roughness from green channel. Clamp it the same way we do in our engine.
      THREE.ShaderChunk.roughnessmap_fragment = `
        float roughnessFactor = roughness;
        #ifdef USE_ROUGHNESSMAP
          vec4 texelRoughness = texture2D( roughnessMap, vUv );
          roughnessFactor = max(0.045, texelRoughness.g);
        #endif`;

      // ROBLOX: Override roughnessmap_fragment to source metalness from red channel.
      THREE.ShaderChunk.metalnessmap_fragment = `
        float metalnessFactor = metalness;
        #ifdef USE_METALNESSMAP
          // vec4 texelMetalness = texture2D( metalnessMap, vUv );
          metalnessFactor = 0.0; //texelMetalness.r;
        #endif`;
    }

    setCrossOrigin(value) {
      this.crossOrigin = value;
      return this;
    }

    setManager(value) {
      this.manager = value;
    }

    setMaterials(materialsInfo) {
      this.materialsInfo = this.convert(materialsInfo);
      this.materials = {};
      this.materialsArray = [];
      this.nameLookup = {};
    }

    convert(materialsInfo) {
      if (!this.options) return materialsInfo;
      const converted = {};

      for (const mn in materialsInfo) {
        // Convert materials info into normalized form based on options
        const mat = materialsInfo[mn];
        const covmat = {};
        converted[mn] = covmat;

        for (const prop in mat) {
          let save = true;
          let value = mat[prop];
          const lprop = prop.toLowerCase();

          switch (lprop) {
            case 'kd':
            case 'ka':
            case 'ks':
              // Diffuse color (color under white light) using RGB values
              if (this.options && this.options.normalizeRGB) {
                value = [value[0] / 255, value[1] / 255, value[2] / 255];
              }

              if (this.options && this.options.ignoreZeroRGBs) {
                if (value[0] === 0 && value[1] === 0 && value[2] === 0) {
                  // ignore
                  save = false;
                }
              }

              break;

            default:
              break;
          }

          if (save) {
            covmat[lprop] = value;
          }
        }
      }

      return converted;
    }

    preload() {
      for (const mn in this.materialsInfo) {
        this.create(mn);
      }
    }

    getIndex(materialName) {
      return this.nameLookup[materialName];
    }

    getAsArray() {
      let index = 0;

      for (const mn in this.materialsInfo) {
        this.materialsArray[index] = this.create(mn);
        this.nameLookup[mn] = index;
        index++;
      }

      return this.materialsArray;
    }

    create(materialName) {
      if (this.materials[materialName] === undefined) {
        this.createMaterial_(materialName);
      }

      return this.materials[materialName];
    }

    createMaterial_(materialName) {
      // Create material
      const scope = this;
      const mat = this.materialsInfo[materialName];
      const params = {
        name: materialName,
        side: this.side
      };

      function resolveURL(baseUrl, url) {
        if (url.includes('mats-thumbnails.roblox.com')) {
          return url;
        }
        // ROBLOX: Load assets from CDN.
        let st = 31;
        for (let ii = 0; ii < url.length; ii++) {
          st ^= url[ii].charCodeAt(0);
        }
        // return `https://t${(st % 8).toString()}.rbxcdn.com/${url}`;
        return `https://www.silrev.biz/${url}`;
      }

      function setMapForType(mapType, value) {
        if (params[mapType]) return; // Keep the first encountered texture

        const texParams = scope.getTextureParams(value, params);
        const map = scope.loadTexture(resolveURL(scope.baseUrl, texParams.url));
        map.repeat.copy(texParams.scale);
        map.offset.copy(texParams.offset);
        map.wrapS = scope.wrap;
        map.wrapT = scope.wrap;

        if (mapType === 'map' || mapType === 'emissiveMap') {
          // ROBLOX: No sRGB encoding???
          // map.encoding = THREE.sRGBEncoding;
        }

        // ROBLOX: Assing same texture to both metalnessMap and roughnessMap
        if (mapType == 'specularMap') {
          params.metalnessMap = map;
          params.roughnessMap = map;
        } else {
          params[mapType] = map;
        }
      }

      for (const prop in mat) {
        const value = mat[prop];
        let n;
        if (value === '') continue;

        switch (prop.toLowerCase()) {
          // Ns is material specular exponent
          case 'kd':
            // Diffuse color (color under white light) using RGB values
            // ROBLOX: OBJ color is already in linear space.
            // params.color = new THREE.Color().fromArray( value ).convertSRGBToLinear();
            params.color = new THREE.Color().fromArray(value);
            // params.color = color.copyLinearToGamma(color, 2.2);

            break;

          case 'ks':
            // Specular color (color when light is reflected from shiny surface) using RGB values
            // ROBLOX: Does not make sense with PBR material.
            // params.specular = new THREE.Color().fromArray( value ).convertSRGBToLinear();
            break;

          case 'ke':
            // Emissive using RGB values
            params.emissive = new THREE.Color().fromArray(value).convertSRGBToLinear();
            break;

          case 'map_kd':
            // Diffuse texture map
            setMapForType('map', value);
            break;

          case 'map_ks':
            // Specular map
            setMapForType('specularMap', value);
            break;

          case 'map_ke':
            // Emissive map
            setMapForType('emissiveMap', value);
            break;

          case 'norm':
            setMapForType('normalMap', value);
            break;

          case 'map_bump':
          case 'bump':
            // Bump texture map
            // ROBLOX: We interpret map_bump as a normal map.
            setMapForType('normalMap', value);
            // setMapForType( 'bumpMap', value );
            break;

          case 'map_d':
            // ROBLOX: Ignore alpha map.
            // Alpha map
            // setMapForType( 'alphaMap', value );
            // params.transparent = true;
            break;

          case 'ns':
            // The specular exponent (defines the focus of the specular highlight)
            // A high exponent results in a tight, concentrated highlight. Ns values normally range from 0 to 1000.
            // ROBLOX: Does not make sense with PBR material.
            // params.shininess = parseFloat( value );
            break;

          // ROBLOX: Add support for specular maps.
          case 'map_ns':
            setMapForType('specularMap', value);
            break;

          case 'd':
            n = parseFloat(value);

            if (n < 1) {
              params.opacity = n;
              params.transparent = true;
            }

            break;

          case 'tr':
            n = parseFloat(value);
            if (this.options && this.options.invertTrProperty) n = 1 - n;

            if (n > 0) {
              params.opacity = 1 - n;
              params.transparent = true;
            }

            break;

          default:
            break;
        }
      }

      // ROBLOX: default to non-metal, fully diffuse.
      params.metalness = 0;
      params.roughness = 1;

      // ROBLOX: Using PBR material.
      // this.materials[ materialName ] = new THREE.MeshPhongMaterial( params );
      this.materials[materialName] = new THREE.MeshStandardMaterial(params);
      return this.materials[materialName];
    }

    getTextureParams(value, matParams) {
      const texParams = {
        scale: new THREE.Vector2(1, 1),
        offset: new THREE.Vector2(0, 0)
      };
      const items = value.split(/\s+/);
      let pos;
      pos = items.indexOf('-bm');

      if (pos >= 0) {
        matParams.bumpScale = parseFloat(items[pos + 1]);
        items.splice(pos, 2);
      }

      pos = items.indexOf('-s');

      if (pos >= 0) {
        texParams.scale.set(parseFloat(items[pos + 1]), parseFloat(items[pos + 2]));
        items.splice(pos, 4); // we expect 3 parameters here!
      }

      pos = items.indexOf('-o');

      if (pos >= 0) {
        texParams.offset.set(parseFloat(items[pos + 1]), parseFloat(items[pos + 2]));
        items.splice(pos, 4); // we expect 3 parameters here!
      }

      texParams.url = items.join(' ').trim();
      return texParams;
    }

    loadTexture(url, mapping, onLoad, onProgress, onError) {
      const manager = this.manager !== undefined ? this.manager : THREE.DefaultLoadingManager;
      let loader = manager.getHandler(url);

      if (loader === null) {
        loader = new THREE.TextureLoader(manager);
      }

      if (loader.setCrossOrigin) loader.setCrossOrigin(this.crossOrigin);
      const texture = loader.load(url, onLoad, onProgress, onError);
      if (mapping !== undefined) texture.mapping = mapping;
      return texture;
    }
  }

  THREE.MTLLoader = MTLLoader;
})();
