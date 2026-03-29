import '../styles/globals.css';
import '../styles/helpers/textHelpers.css';
import 'bootstrap/dist/css/bootstrap.min.css';
// Roblox CSS
import '../styles/roblox/icons.css';
import Navbar from '../components/navbar';
import React, {useEffect} from 'react';
import Head from 'next/head';
import Footer from '../components/footer';
import NextNProgress from "nextjs-progressbar";
import LoginModalStore from '../stores/loginModal';
import AuthenticationStore from '../stores/authentication';
import NavigationStore from '../stores/navigation';
import {getTheme, themeType, getFontStyle, fontStyle} from '../services/theme';
import {toFreaky} from '../services/freakyUnicode';
import MainWrapper from '../components/mainWrapper';
import GlobalAlert from '../components/globalAlert';
import ThumbnailStore from "../stores/thumbnailStore";
import getFlag from "../lib/getFlag";
import Chat from "../components/chat";
import FeedbackStore from "../stores/feedback";
import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime.js'

if (typeof window !== 'undefined') {
    console.log(String.raw`
      _______      _________      _____       ______     _
     / _____ \    |____ ____|    / ___ \     | ____ \   | |
    / /     \_\       | |       / /   \ \    | |   \ \  | |
    | |               | |      / /     \ \   | |   | |  | |
    \ \______         | |      | |     | |   | |___/ /  | |
     \______ \        | |      | |     | |   |  ____/   | |
            \ \       | |      | |     | |   | |        | |
     _      | |       | |      \ \     / /   | |        |_|
    \ \_____/ /       | |       \ \___/ /    | |         _
     \_______/        |_|        \_____/     |_|        |_|

     Keep your account safe! Do not paste any text here.`);
}

function RobloxApp({Component, pageProps}) {
    // set theme:
    // jss globals apparently don't support parameters/props, so the only way to do a dynamic global style is to either append a <style> element, use setAttribute(), or append a css file.
    // @ts-ignore
    const isChristmas = false
    useEffect(() => {
		// for font switch
        const bodyEl = document.body;
        if (!bodyEl) return;
        const font = getFontStyle();
        if (font === fontStyle.freaky) {
            const transformFn = toFreaky;
            const walk = document.createTreeWalker(bodyEl, NodeFilter.SHOW_TEXT, null, false);
            let node;
            while ((node = walk.nextNode())) {
                if (
                     node.parentNode &&
                    (node.parentNode.tagName === 'INPUT' ||
                     node.parentNode.tagName === 'TEXTAREA' ||
                     node.parentNode.isContentEditable)
                ) continue;

                node.nodeValue = transformFn(node.nodeValue);
            }
        } else {
			const safeClass = (name) => `font-${name.replace(/\s+/g, '-')}`;
            Object.values(fontStyle).forEach(f => bodyEl.classList.remove(safeClass(f)));
            bodyEl.classList.add(safeClass(font));
        }
        // const el = typeof window !== 'undefined' && document.getElementsByTagName('body');
        // if (el && el.length) {
        //   const theme = getTheme();
        //   const divBackground = theme === themeType.obc2016 ? 'url(/img/Unofficial/obc_theme_2016_bg.png) repeat-x #222224' : isChristmas ? 'url(/img/holiday/blue-snow.png) repeat' : document.getElementById('theme-2016-enabled') ? 'var(--background-color)' : 'var(--white-color)';
        //   el[0].setAttribute('style', 'background: ' + divBackground);
        //   if (theme === themeType.obc2016) {
        //     document.documentElement.style.setProperty('--text-color-primary', '#fff');
        //     document.documentElement.style.setProperty('--text-color-secondary', '#5a5a5a');
        //     document.documentElement.style.setProperty('--white-color', '#191919');
        //     document.documentElement.style.setProperty('--background-color', '#393939');
        //     document.documentElement.style.setProperty('--text-color-secondary-dark', '#b8b8b8');
        //     document.documentElement.setAttribute('data-bs-theme', 'dark');
        //     document.documentElement.style.setProperty('--text-color-quinary', '#5b5b5b');
        //   }
        //   if (isChristmas) {
        //     document.documentElement.style.setProperty('--primary-color', 'rgb(174,0,62)');
        //     document.documentElement.style.setProperty('--secondary-color', 'rgb(150,0,51)');
        //     document.documentElement.style.setProperty('--primary-color-hover', 'rgb(210,0,87)');
        //   }
        // }
		
        dayjs.extend(relativeTime);
        const el = typeof window !== 'undefined' && document.getElementsByTagName('body');
        if (el && el.length) {
            const theme = getTheme();
            const divBackground =
                theme === themeType.obc2016
                ?
                'url(/img/Unofficial/obc_theme_2016_bg.png) repeat-x #222224'
                :
                document.getElementById('theme-2016-enabled')
                ?
                '#e3e3e3'
                :
                '#fff'
            ;
            el[0].setAttribute('style', 'background: ' + divBackground);
        }
    }, [pageProps]);
    
    return <div style={pageProps.disableWebsiteTheming ? {minHeight: '100vh'} : null}>
        <Head>
            <link rel="preconnect" href="https://fonts.googleapis.com"/>
            <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin={''}/>
            <title>{pageProps.title || 'Marine'}</title>
            <link rel='icon' type="image/vnd.microsoft.icon" href='/favicon.ico'/>
            <meta name='viewport' content='width=device-width, initial-scale=1'/>
        </Head>
        <AuthenticationStore.Provider>
            {pageProps.disableWebsiteTheming ? null : <>
                <LoginModalStore.Provider>
                    <NavigationStore.Provider>
                        <Navbar/>
                    </NavigationStore.Provider>
                </LoginModalStore.Provider>
                <GlobalAlert/>
            </>}
            <FeedbackStore.Provider>
                <MainWrapper mainFlex={pageProps.disableWebsiteTheming}>
                    {getFlag('clientSideRenderingEnabled', false) ?
                        <NextNProgress options={{showSpinner: true}} color='var(--primary-color)' height={4}/> : null}
                    <ThumbnailStore.Provider>
                        <Component {...pageProps} />
                        <Chat/>
                    </ThumbnailStore.Provider>
                </MainWrapper>
            </FeedbackStore.Provider>
            {pageProps.disableWebsiteTheming ? null : <Footer/>}
        </AuthenticationStore.Provider>
    </div>
}

export default RobloxApp;
