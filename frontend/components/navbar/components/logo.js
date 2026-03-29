import { createUseStyles } from "react-jss";
import { getTheme, themeType, getLogoStyle, logoStyle } from "../../../services/theme";
import NavigationStore from "../../../stores/navigation";

const faviconImages = {
    [logoStyle.Default]: 'url(/img/favicon.png)',
    [logoStyle.MarineModern]: 'url(/img/marine_favicon.png)',
    [logoStyle.Pekora]: 'url(/img/pekora_favicon.png)',
    [logoStyle.PekoraBlue]: 'url(/img/pekora_blue_favicon.png)',
    [logoStyle.ProjectX]: 'url(/img/projectx_favicon.png)',
    [logoStyle.Silverium]: 'url(/img/silverium_favicon.png)',
    [logoStyle.Roblox2009]: 'url(/img/roblox2009_favicon.svg)',
    [logoStyle.Roblox2013]: 'url(/img/roblox2013_favicon.png)',
	[logoStyle.Roblox2016]: 'url(/img/roblox2016_favicon.png)',
    [logoStyle.Roblox2017]: 'url(/img/roblox2017_favicon.png)',
    [logoStyle.Roblox2019]: 'url(/img/roblox2019_favicon.png)',
};

const logoImages = {
    [logoStyle.Default]: 'url(/img/logo.png)',
    [logoStyle.MarineModern]: 'url(/img/marine_modern.png)',
    [logoStyle.Pekora]: 'url(/img/pekora.png)',
    [logoStyle.PekoraBlue]: 'url(/img/pekora_blue.png)',
    [logoStyle.ProjectX]: 'url(/img/projectx.png)',
    [logoStyle.Silverium]: 'url(/img/silverium.png)',
    [logoStyle.Roblox2009]: 'url(/img/roblox2009.png)',
    [logoStyle.Roblox2013]: 'url(/img/roblox2013.png)',
	[logoStyle.Roblox2016]: 'url(/img/roblox2016.png)',
    [logoStyle.Roblox2017]: 'url(/img/roblox2017.png)',
    [logoStyle.Roblox2019]: 'url(/img/roblox2019.png)',
};


const useLogoStyles = createUseStyles({
  imgDesktop: {
    width: '122px',
    minWidth: '122px',
    maxWidth: '122px',
    height: '40px',
    // backgroundImage: `url(/img/roblox_logo.svg)`,
    //backgroundImage: 'url(/img/holiday/projex_logo_studio.png)',
    backgroundImage: p => logoImages[p.logo] || logoImages[logoStyle.Default],
    // backgroundSize: '122px 30px',
    backgroundSize: "100% auto",
    display: 'none',
    '@media(min-width: 1325px)': {
      display: 'block',
    },
    backgroundRepeat: 'no-repeat',
    backgroundPosition: 'center'
  },
  imgMobile: {
    //backgroundImage: `url(/img/logo_R.svg)`,
	backgroundImage: p => faviconImages[p.logo] || faviconImages[logoStyle.Default],
    width: '30px',
    height: '30px',
    display: 'block',
    backgroundSize: '30px',
    backgroundRepeat: 'no-repeat',
    backgroundPosition: 'center',
    marginLeft: '6px',
    '@media(min-width: 1325px)': {
      display: 'none',
    },
  },
  imgMobileWrapper: {
    marginLeft: '12px',
  },
  col: {
    maxWidth: '118px',
    padding: '0',
    margin: '0 12px',
    display: 'flex',
    justifyContent: 'start',
    alignItems: 'center',
    '@media(max-width: 1324px)': {
      margin: '0 6px',
      width: 'auto',
    },
    '@media(max-width: 992px)': {
      width: '20%',
      margin: 0,
      marginBottom: '6px',
    },
  },
  openSideNavMobile: {
    display: 'none',
    '@media(max-width: 1324px)': {
      display: 'block',
      float: 'left',
      height: '30px',
      width: '30px',
      cursor: 'pointer',
    },
  },
});
const Logo = () => {
  const s = useLogoStyles({ theme: getTheme(), logo: getLogoStyle() });
  const navStore = NavigationStore.useContainer();

  return <div className={`${s.col} col-3 col-lg-3`}>
    <div className={s.openSideNavMobile + ' icon-menu'} onClick={() => {
      navStore.setIsSidebarOpen(!navStore.isSidebarOpen);
    }}></div>
    <a className={s.imgDesktop} href='/home'></a>
    <a className={s.imgMobile} href='/home'></a>
    {/*<div className={s.imgMobileWrapper}>
    
    </div>*/}
  </div>
}

export default Logo;