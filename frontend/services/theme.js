const themeType = {
	koroneholyfuck67: 'koroneholyfuck67',
    obc2016: 'obc2016',
    light: 'light',
    default: 'light',
}

const avPageStyleType = {
    Modern: 'Modern',
    Legacy: 'Legacy',
}

const catalogPageStyle = {
    Modern: 'Modern',
    Legacy: 'Legacy',
}

const fontStyle = {
    Default: 'Default',
    SourceSans: 'SourceSans',
	freaky: 'freaky',
}

const logoStyle = {
    Default: 'Default',
    MarineModern: 'MarineModern',
	Pekora: 'Pekora',
	PekoraBlue: 'PekoraBlue',
	ProjectX: 'ProjectX',
	Silverium: 'Silverium',
	Roblox2009: 'Roblox2009',
	Roblox2013: 'Roblox2013',
	Roblox2016: 'Roblox2016',
	Roblox2017: 'Roblox2017',
	Roblox2019: 'Roblox2019',
}

const isLocalStorageAvailable = (() => {
    // @ts-ignore
    if (!process.browser) return false;
    if (typeof window === 'undefined' || !window.localStorage || !window.localStorage.getItem || !window.localStorage.setItem) return false;
    
    return true;
})()

const getTheme = () => {
    if (!isLocalStorageAvailable) return themeType.default;
    
    let value = localStorage.getItem('rbx_theme_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(themeType).includes(value)) return themeType.default;
    return themeType[value];
}

const setTheme = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_theme_v1', themeString)
}

const getAvPageStyle = () => {
    if (!isLocalStorageAvailable) return avPageStyleType.default;
    
    let value = localStorage.getItem('rbx_av_page_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(avPageStyleType).includes(value)) return avPageStyleType.default;
    return avPageStyleType[value];
}

const setAvPageStyle = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_av_page_style_v1', themeString)
}

const getCatalogPageStyle = () => {
    if (!isLocalStorageAvailable) return catalogPageStyle["Modern"];
    
    let value = localStorage.getItem('rbx_cat_page_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.getOwnPropertyNames(catalogPageStyle).includes(value)) return catalogPageStyle["Modern"];
    return catalogPageStyle[value];
}

const setCatalogPageStyle = (themeString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_cat_page_style_v1', themeString);
}

const getFontStyle = () => {
    if (!isLocalStorageAvailable) return fontStyle.Default;

    let value = localStorage.getItem('rbx_font_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.values(fontStyle).includes(value)) 
        return fontStyle.Default;
    return value;
}

const setFontStyle = (fontString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_font_style_v1', fontString);
}

const getLogoStyle = () => {
    if (!isLocalStorageAvailable) return fontStyle.Default;

    let value = localStorage.getItem('rbx_logo_style_v1');
    // validate
    if (typeof value !== 'string' || !Object.values(fontStyle).includes(value)) 
        return fontStyle.Default;
    return value;
}

const setLogoStyle = (logoString) => {
    if (!isLocalStorageAvailable) return;
    localStorage.setItem('rbx_logo_style_v1', logoString);
}

export {
    getTheme,
    setTheme,

    getAvPageStyle,
    setAvPageStyle,

    getCatalogPageStyle,
    setCatalogPageStyle,

    getFontStyle,
    setFontStyle,

	getLogoStyle,
	setLogoStyle,

    themeType,
    avPageStyleType,
    catalogPageStyle,
    fontStyle,
	logoStyle,
}