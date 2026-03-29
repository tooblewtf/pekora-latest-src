import Document from 'next/document'
import React from 'react'
import {SheetsRegistry, JssProvider, createGenerateId} from 'react-jss'

export default class JssDocument extends Document {
    static async getInitialProps(ctx) {
        const registry = new SheetsRegistry()
        const generateId = createGenerateId()
        const originalRenderPage = ctx.renderPage
        ctx.renderPage = () =>
            originalRenderPage({
                enhanceApp: (App) => (props) => (
                    <JssProvider registry={registry} generateId={generateId}>
                        <App {...props} />
                    </JssProvider>
                ),
            })
        
        const initialProps = await Document.getInitialProps(ctx)
        
        return {
            ...initialProps,
            styles: (
                <>
                    {initialProps.styles}
                    <link
                        href="https://fonts.googleapis.com/css2?family=Source+Sans+Pro:ital,wght@0,200;0,300;0,400;0,600;0,700;0,900;1,200;1,300;1,400;1,600;1,700;1,900&amp;display=swap"
                        rel="stylesheet"/>
                    <link href="https://www.silrev.biz/fonts/gotham1.css" rel="stylesheet"/>
                    <style id="server-side-styles">{registry.toString()}</style>
                </>
            ),
        }
    }
}
