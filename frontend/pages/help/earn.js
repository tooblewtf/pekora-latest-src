'use client'

import EarnHelpPage from "../../components/help/earn";
import Theme2016 from "../../components/theme2016";
import Head from "next/head";

const EarnHelp = props => {
    return (
        <Theme2016>
            <Head>
                <link rel="preload" href="/markdown/earnRoTix.md" as="fetch" crossOrigin="anonymous" />
            </Head>
            <EarnHelpPage />
        </Theme2016>
    );
}

export const getStaticProps = () => {
    return {
        props: {
            title: 'Earning Robux - Marine',
            disableWebsiteTheming: true,
        },
    };
};

export default EarnHelp;