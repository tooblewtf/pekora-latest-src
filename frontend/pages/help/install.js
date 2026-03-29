'use client'

import InstallHelpPage from "../../components/help/install";
import Theme2016 from "../../components/theme2016";
import Head from "next/head";

const InstallHelp = props => {
    return (
        <Theme2016>
            <Head>
                <link rel="preload" href="/markdown/errorCode6.md" as="fetch" crossOrigin="anonymous" />
                <link rel="preload" href="/markdown/trojan.md" as="fetch" crossOrigin="anonymous" />
                <link rel="preload" href="/markdown/msvcp140.md" as="fetch" crossOrigin="anonymous" />
                <link rel="preload" href="/markdown/installPlay.md" as="fetch" crossOrigin="anonymous" />
            </Head>
            <InstallHelpPage />
        </Theme2016>
    );
}

export const getStaticProps = () => {
    return {
        props: {
            title: 'Installation Help - Marine',
            disableWebsiteTheming: true,
        },
    };
};

export default InstallHelp;